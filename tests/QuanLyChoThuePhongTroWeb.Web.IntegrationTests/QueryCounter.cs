using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace QuanLyChoThuePhongTroWeb.Web.IntegrationTests
{
    // Đếm số lệnh SQL EF Core thực thi trong khoảng đo, qua DiagnosticListener có sẵn của EF Core (không thêm thư viện,
    // không sửa fixture). Đếm toàn tiến trình: chỉ dùng trong test thuộc WebTestCollection (các test chạy lần lượt)
    // và chỉ bao quanh đúng request cần đo.
    public sealed class QueryCounter : IObserver<DiagnosticListener>, IDisposable
    {
        private const string EfListenerName = "Microsoft.EntityFrameworkCore";
        private const string CommandExecuted = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
        private const string CommandError = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";

        private readonly IDisposable _allListeners;
        private readonly ConcurrentBag<IDisposable> _subscriptions = new();
        private int _count;
        private volatile bool _active;

        public QueryCounter()
        {
            _allListeners = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public int Count => Volatile.Read(ref _count);

        // Đo số lệnh SQL trong lúc chạy action (thường là một request HTTP).
        public async System.Threading.Tasks.Task<int> MeasureAsync(Func<System.Threading.Tasks.Task> action)
        {
            Interlocked.Exchange(ref _count, 0);
            _active = true;
            try
            {
                await action();
            }
            finally
            {
                _active = false;
            }

            return Count;
        }

        void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
        {
            if (listener.Name != EfListenerName)
            {
                return;
            }

            _subscriptions.Add(listener.Subscribe(new EventObserver(this), name => name == CommandExecuted || name == CommandError));
        }

        void IObserver<DiagnosticListener>.OnCompleted() { }

        void IObserver<DiagnosticListener>.OnError(Exception error) { }

        public void Dispose()
        {
            _allListeners.Dispose();
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }

        private sealed class EventObserver : IObserver<KeyValuePair<string, object?>>
        {
            private readonly QueryCounter _owner;

            public EventObserver(QueryCounter owner)
            {
                _owner = owner;
            }

            public void OnNext(KeyValuePair<string, object?> value)
            {
                if (_owner._active)
                {
                    Interlocked.Increment(ref _owner._count);
                }
            }

            public void OnCompleted() { }

            public void OnError(Exception error) { }
        }
    }
}
