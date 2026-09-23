using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Infrastructure.Communications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BCKash.Api.IntegrationTests;

/// <summary>
/// Completes the mandatory email/SMS OTP step (see AuthService.LoginAsync) by reading the code
/// back out of the RecordingEmailSender test fake, so individual tests don't need to know the
/// OTP flow's internals just to get past login.
/// </summary>
public static class LoginTestHelper
{
    public static async Task<OtpVerifyResponse> LoginAndVerifyOtpAsync(WebApplicationFactory<Program> factory, HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.True(loginResponse.IsSuccessStatusCode, await loginResponse.Content.ReadAsStringAsync());
        var challenge = await loginResponse.Content.ReadFromJsonAsync<OtpChallengeResponse>();

        var emailSender = (RecordingEmailSender)factory.Services.GetRequiredService<IEmailSender>();
        var otpEmail = emailSender.Sent.Last(e => e.ToAddress.Equals(email, StringComparison.OrdinalIgnoreCase));
        var code = Regex.Match(otpEmail.Body, @"verification code is (\d{6})").Groups[1].Value;

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/login/otp/verify", new OtpVerifyRequest(challenge!.ChallengeToken, code));
        Assert.True(verifyResponse.IsSuccessStatusCode, await verifyResponse.Content.ReadAsStringAsync());
        return (await verifyResponse.Content.ReadFromJsonAsync<OtpVerifyResponse>())!;
    }
}
