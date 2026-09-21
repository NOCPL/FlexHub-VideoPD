using Livekit.Server.Sdk.Dotnet;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services;

public class LiveKitService(IConfiguration config, ILogger<LiveKitService> logger)
{
    public string WsUrl => config["LiveKit:Url"] ?? "ws://127.0.0.1:7880";

    public string CreateParticipantToken(
        Meeting meeting,
        string identity,
        string displayName,
        string role,
        IDictionary<string, string>? extraAttributes = null)
    {
        var attributes = new Dictionary<string, string>
        {
            ["role"] = role,
            ["bank"] = meeting.Bank,
            ["branch"] = meeting.Branch,
            ["groupId"] = meeting.GroupId,
            ["memberId"] = meeting.MemberId
        };
        if (extraAttributes != null)
        {
            foreach (var (key, value) in extraAttributes)
            {
                attributes[key] = value;
            }
        }

        var token = new AccessToken(config["LiveKit:ApiKey"], config["LiveKit:ApiSecret"])
            .WithIdentity(identity)
            .WithName(displayName)
            .WithTtl(TimeSpan.FromHours(4))
            .WithAttributes(attributes)
            .WithMetadata($"{{\"role\":\"{role}\",\"bank\":\"{meeting.Bank}\",\"branch\":\"{meeting.Branch}\",\"groupId\":\"{meeting.GroupId}\",\"memberId\":\"{meeting.MemberId}\"}}")
            .WithGrants(new VideoGrants
            {
                RoomJoin = true,
                Room = meeting.LiveKitRoomName,
                CanPublish = true,
                CanSubscribe = true,
                CanPublishData = true,
                RoomAdmin = role is Roles.CreditOfficer or Roles.Admin,
                CanUpdateOwnMetadata = true
            });
        return token.ToJwt();
    }

    public string RecordingRelativePath(Meeting meeting, int sequence) =>
        $"recordings/{Safe(meeting.Bank)}/{Safe(meeting.Branch)}/{Safe(meeting.GroupId)}/{Safe(meeting.MemberId)}/{meeting.Id:N}/seg-{sequence}.mp4";

    public async Task<(string? EgressId, string? Error)> StartRoomRecordingAsync(Meeting meeting, int sequence)
    {
        var httpUrl = config["LiveKit:HttpUrl"] ?? "http://127.0.0.1:7880";
        var recordingsPath = config["Storage:RecordingsPath"] ?? "/workspace/data/recordings";
        var relative = $"{Safe(meeting.Bank)}/{Safe(meeting.Branch)}/{Safe(meeting.GroupId)}/{Safe(meeting.MemberId)}/{meeting.Id:N}/seg-{sequence}.mp4";
        var filepath = Path.Combine(recordingsPath, relative).Replace('\\', '/');
        Directory.CreateDirectory(Path.GetDirectoryName(filepath)!);

        try
        {
            var client = new EgressServiceClient(
                httpUrl,
                config["LiveKit:ApiKey"],
                config["LiveKit:ApiSecret"]);

            var request = new RoomCompositeEgressRequest
            {
                RoomName = meeting.LiveKitRoomName,
                Layout = "grid"
            };
            request.FileOutputs.Add(new EncodedFileOutput
            {
                FileType = EncodedFileType.Mp4,
                Filepath = filepath
            });

            var info = await client.StartRoomCompositeEgress(request);
            logger.LogInformation("Started egress {EgressId} segment {Seq} for room {Room}", info.EgressId, sequence, meeting.LiveKitRoomName);
            return (info.EgressId, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Egress not available; meeting {Id} segment {Seq} has no media file", meeting.Id, sequence);
            return (null, ex.Message);
        }
    }

    public async Task StopRecordingAsync(string egressId)
    {
        var httpUrl = config["LiveKit:HttpUrl"] ?? "http://127.0.0.1:7880";
        try
        {
            var client = new EgressServiceClient(
                httpUrl,
                config["LiveKit:ApiKey"],
                config["LiveKit:ApiSecret"]);
            await client.StopEgress(new StopEgressRequest { EgressId = egressId });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop egress {EgressId}", egressId);
        }
    }

    public async Task DeleteRoomAsync(string roomName)
    {
        var httpUrl = config["LiveKit:HttpUrl"] ?? "http://127.0.0.1:7880";
        try
        {
            var client = new RoomServiceClient(
                httpUrl,
                config["LiveKit:ApiKey"],
                config["LiveKit:ApiSecret"]);
            await client.DeleteRoom(new DeleteRoomRequest { Room = roomName });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete LiveKit room {Room}", roomName);
        }
    }

    public WebhookEvent ParseWebhook(string body, string authorization)
    {
        var receiver = new WebhookReceiver(config["LiveKit:ApiKey"], config["LiveKit:ApiSecret"]);
        return receiver.Receive(body, authorization);
    }

    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "_";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
