namespace DemoWithNuGet.Holidays.Actors;

using Akka.Actor;

/// <summary>
/// An Akka.NET actor that processes a single year's holiday data for a given country.
/// </summary>
public sealed class HolidayWorkerActor : ReceiveActor
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri("https://holidays.api")
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayWorkerActor"/> class.
    /// </summary>
    public HolidayWorkerActor()
    {
        ReceiveAsync<ProcessYear>(async msg =>
        {
            try
            {
                var response = await _client.GetAsync($"/{msg.Country}/{msg.Year}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                Sender.Tell(new YearProcessed(msg.Year, content));
            }
            catch (Exception ex)
            {
                Sender.Tell(new Status.Failure(ex));
            }
        });
    }

    /// <summary>
    /// Message to process a specific year and country.
    /// </summary>
    /// <param name="Year">The year to process.</param>
    /// <param name="Country">The country code.</param>
    public sealed record ProcessYear(int Year, string Country);

    /// <summary>
    /// Response message containing the processed year and holiday content.
    /// </summary>
    /// <param name="Year">The year that was processed.</param>
    /// <param name="Content">The holiday data content.</param>
    public sealed record YearProcessed(int Year, string Content);
}
