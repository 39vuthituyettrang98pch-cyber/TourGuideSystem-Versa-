using AdminWeb.Models;

namespace AdminWeb.ViewModels;

public sealed class UsageHistoryViewModel
{
    public List<VisitorPlaybackLog> Logs { get; set; } = [];
}
