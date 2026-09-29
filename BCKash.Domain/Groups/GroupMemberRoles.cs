namespace BCKash.Domain.Groups;

public static class GroupMemberRoles
{
    public const string Leader = "leader";
    public const string Assistant = "assistant";
    public const string Organizer = "organizer";
    public const string Member = "member";

    /// <summary>The role of the member at <paramref name="index"/> (0-based) in onboarding order.</summary>
    public static string ForPosition(int index) => index switch
    {
        0 => Leader,
        1 => Assistant,
        2 => Organizer,
        _ => Member,
    };
}
