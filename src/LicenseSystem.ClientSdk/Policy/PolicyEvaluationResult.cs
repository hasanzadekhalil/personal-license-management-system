using LicenseSystem.Core.Enums;

namespace LicenseSystem.ClientSdk.Policy;

public class PolicyEvaluationResult
{
    public bool IsAuthorized { get; set; }
    public LicenseStatus Status { get; set; }
    public TimeSpan? RemainingGracePeriod { get; set; }
    public string Message { get; set; } = string.Empty;

    public static PolicyEvaluationResult Authorized(LicenseStatus status, string message = "License valid and active.")
    {
        return new PolicyEvaluationResult
        {
            IsAuthorized = true,
            Status = status,
            Message = message
        };
    }

    public static PolicyEvaluationResult Grace(TimeSpan remainingGrace, string message = "Operating under authorized 6-hour offline grace period.")
    {
        return new PolicyEvaluationResult
        {
            IsAuthorized = true,
            Status = LicenseStatus.Active,
            RemainingGracePeriod = remainingGrace,
            Message = message
        };
    }

    public static PolicyEvaluationResult Locked(LicenseStatus status, string message)
    {
        return new PolicyEvaluationResult
        {
            IsAuthorized = false,
            Status = status,
            Message = message
        };
    }
}
