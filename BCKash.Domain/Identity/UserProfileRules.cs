namespace BCKash.Domain.Identity;

/// <summary>What a staff member must fill in on their own profile before their onboarding counts as complete.</summary>
public static class UserProfileRules
{
    public static IReadOnlyList<string> MissingFields(User user)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(user.FirstName)) missing.Add("firstName");
        if (string.IsNullOrWhiteSpace(user.LastName)) missing.Add("lastName");
        if (string.IsNullOrWhiteSpace(user.Phone)) missing.Add("phone");
        if (user.Gender == Gender.Unspecified) missing.Add("gender");
        if (user.DateOfBirth is null) missing.Add("dateOfBirth");
        if (string.IsNullOrWhiteSpace(user.Address)) missing.Add("address");
        if (string.IsNullOrWhiteSpace(user.NextOfKinName)) missing.Add("nextOfKinName");
        if (string.IsNullOrWhiteSpace(user.NextOfKinPhone)) missing.Add("nextOfKinPhone");
        if (string.IsNullOrWhiteSpace(user.NextOfKinRelationship)) missing.Add("nextOfKinRelationship");
        if (string.IsNullOrWhiteSpace(user.BankName)) missing.Add("bankName");
        if (string.IsNullOrWhiteSpace(user.BankAccountNumber)) missing.Add("bankAccountNumber");
        if (string.IsNullOrWhiteSpace(user.BankAccountName)) missing.Add("bankAccountName");
        return missing;
    }
}
