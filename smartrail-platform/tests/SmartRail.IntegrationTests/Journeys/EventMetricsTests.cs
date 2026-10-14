using System.Diagnostics.Metrics;
using System.Net;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>smartrail.events.received counts every report by outcome (spec section 7), and the histogram records the NFR-04 part.</summary>
public sealed class EventMetricsTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    [Fact]
    public async Task Recorded_duplicate_and_rejected_reports_are_each_counted()
    {
        var counts = new Dictionary<string, long>();
        var visible = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name is "smartrail.events.received" or "smartrail.events.report_to_visible_seconds")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    lock (counts)
                    {
                        counts[(string)tag.Value!] = counts.GetValueOrDefault((string)tag.Value!) + value;
                    }
                }
            }
        });
        listener.SetMeasurementEventCallback<double>((_, value, _, _) =>
        {
            lock (visible)
            {
                visible.Add(value);
            }
        });
        listener.Start();

        var id = Guid.NewGuid();
        Clock.SetUtcNow(Local(8, 30)); // two minutes after the departure, so the histogram has something to say
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28), clientEventId: id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Report(Stops[0], Local(8, 28), clientEventId: id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Report(Stops[1], Local(8, 29), token: UnassignedOperatorToken)).StatusCode);

        lock (counts)
        {
            Assert.Equal(1, counts["recorded"]);
            Assert.Equal(1, counts["duplicate"]);
            Assert.Equal(1, counts["rejected"]);
        }

        lock (visible)
        {
            Assert.Equal([120.0], visible); // 08:28 to the 08:30 commit
        }
    }
}
