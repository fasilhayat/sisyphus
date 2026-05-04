namespace Demo.Holidays.Actors;

using Akka.Actor;

public sealed class HolidayWorkerActor : ReceiveActor
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = new Uri("https://holidays.api")
    };

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

    public sealed record ProcessYear(int Year, string Country);
    public sealed record YearProcessed(int Year, string Content);
}
