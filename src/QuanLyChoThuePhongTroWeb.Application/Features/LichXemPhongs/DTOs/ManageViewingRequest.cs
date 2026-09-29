namespace QuanLyChoThuePhongTroWeb.Application.Features.LichXemPhongs.DTOs;

public enum ViewingStaffAction { Cancel, Complete, Reschedule }
public sealed record ManageViewingRequest(ViewingStaffAction Action, string Reason,
    DateTime? NewTimeUtc, int? NewSlotId = null);
