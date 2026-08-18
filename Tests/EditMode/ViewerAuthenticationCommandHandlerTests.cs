using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.ViewerAuthentication.Tests
{
    public sealed class ViewerAuthenticationCommandHandlerTests
    {
        [Test]
        public async Task UpdateCommandNormalizesTokenAndPublishesSanitizedStatus()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            var host = new TestHost(session);
            var publisher = new RecordingPublisher();
            var handler =
                new ViewerAuthenticationCommandHandler<TestHost>(publisher);
            string expiry = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
            var command = new CommandEnvelope(
                ViewerAuthenticationCommandNames.UpdateAccessToken,
                new JObject
                {
                    ["access_token"] = "Bearer command-token",
                    ["expires_at_utc"] = expiry
                });

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    host,
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.EqualTo("command-token"));
            Assert.That(result.Payload.Value<bool>("has_access_token"), Is.True);
            Assert.That(result.Payload.ToString(),
                Does.Not.Contain("command-token"));
            Assert.That(publisher.EventName,
                Is.EqualTo(ViewerAuthenticationEventNames.AccessTokenUpdated));
            Assert.That(publisher.Status.HasAccessToken, Is.True);
            Assert.That(publisher.Status.ExpiresAtUtc.HasValue, Is.True);
        }

        [Test]
        public async Task LegacyUpdateAliasRemainsSupported()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            var handler =
                new ViewerAuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                ViewerAuthenticationCommandNames.UpdateAccessTokenLegacy,
                new JObject { ["access_token"] = "legacy-token" });

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    new TestHost(session),
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.EqualTo("legacy-token"));
            Assert.That(result.Payload.ToString(),
                Does.Not.Contain("legacy-token"));
        }

        [Test]
        public async Task InvalidExpiryIsRejectedBeforeTokenMutation()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            var handler =
                new ViewerAuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                ViewerAuthenticationCommandNames.UpdateAccessToken,
                new JObject
                {
                    ["access_token"] = "should-not-apply",
                    ["expires_at_utc"] = "not-a-timestamp"
                });

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    new TestHost(session),
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(result.Payload.ToString(),
                Does.Not.Contain("should-not-apply"));
        }

        [Test]
        public async Task UpdateCommandUsesStrictExplicitPayloadReads()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            var handler =
                new ViewerAuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                ViewerAuthenticationCommandNames.UpdateAccessToken,
                new JObject
                {
                    ["access_token"] = new JArray("must-not-coerce")
                });

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    new TestHost(session),
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("invalid_payload"));
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(result.Payload.ToString(),
                Does.Not.Contain("must-not-coerce"));
        }

        [Test]
        public async Task ClearCommandPublishesOnlyMissingStatus()
        {
            ViewerAuthenticationSession session =
                ViewerAuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("clear-me");
            var publisher = new RecordingPublisher();
            var handler =
                new ViewerAuthenticationCommandHandler<TestHost>(publisher);
            var command = new CommandEnvelope(
                ViewerAuthenticationCommandNames.ClearAccessToken);

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    new TestHost(session),
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(publisher.EventName,
                Is.EqualTo(ViewerAuthenticationEventNames.AccessTokenCleared));
            Assert.That(publisher.Status.Status,
                Is.EqualTo(ViewerAuthenticationStatus.Missing));
            Assert.That(result.Payload.ToString(), Does.Not.Contain("clear-me"));
        }

        private sealed class TestHost : IViewerAuthenticationHost
        {
            internal TestHost(IViewerAuthenticationSession session)
            {
                AuthenticationSession = session;
            }

            public IViewerAuthenticationSession AuthenticationSession
            {
                get;
            }
        }

        private sealed class RecordingPublisher :
            IViewerAuthenticationEventPublisher
        {
            public string EventName { get; private set; }
            public ViewerAuthenticationStatusSnapshot Status
            {
                get;
                private set;
            }

            public Task PublishAsync(
                string eventName,
                ViewerAuthenticationStatusSnapshot status,
                CancellationToken cancellationToken =
                    default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                EventName = eventName;
                Status = status;
                return Task.CompletedTask;
            }
        }
    }
}
