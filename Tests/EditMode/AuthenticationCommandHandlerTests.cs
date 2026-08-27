using System;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.CommandRouting;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Deucarian.Authentication.Tests
{
    public sealed class AuthenticationCommandHandlerTests
    {
        [Test]
        public async Task UpdateCommandNormalizesTokenAndPublishesSanitizedStatus()
        {
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            var host = new TestHost(session);
            var publisher = new RecordingPublisher();
            var handler =
                new AuthenticationCommandHandler<TestHost>(publisher);
            string expiry = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
            var command = new CommandEnvelope(
                AuthenticationCommandNames.UpdateAccessToken,
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
                Is.EqualTo(AuthenticationEventNames.AccessTokenUpdated));
            Assert.That(publisher.Status.HasAccessToken, Is.True);
            Assert.That(publisher.Status.ExpiresAtUtc.HasValue, Is.True);
        }

        [Test]
        public async Task LegacyUpdateAliasRemainsSupported()
        {
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            var handler =
                new AuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                AuthenticationCommandNames.UpdateAccessTokenLegacy,
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
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            var handler =
                new AuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                AuthenticationCommandNames.UpdateAccessToken,
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
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            var handler =
                new AuthenticationCommandHandler<TestHost>();
            var command = new CommandEnvelope(
                AuthenticationCommandNames.UpdateAccessToken,
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
            AuthenticationSession session =
                AuthenticationSession.CreateTransient();
            await session.ReplaceAccessTokenAsync("clear-me");
            var publisher = new RecordingPublisher();
            var handler =
                new AuthenticationCommandHandler<TestHost>(publisher);
            var command = new CommandEnvelope(
                AuthenticationCommandNames.ClearAccessToken);

            CommandResult result = await handler.HandleAsync(
                new CommandExecutionContext<TestHost>(
                    new TestHost(session),
                    command,
                    command.CommandName),
                CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.AccessToken, Is.Null);
            Assert.That(publisher.EventName,
                Is.EqualTo(AuthenticationEventNames.AccessTokenCleared));
            Assert.That(publisher.Status.Status,
                Is.EqualTo(AuthenticationStatus.Missing));
            Assert.That(result.Payload.ToString(), Does.Not.Contain("clear-me"));
        }

        private sealed class TestHost : IAuthenticationHost
        {
            internal TestHost(IAuthenticationSession session)
            {
                AuthenticationSession = session;
            }

            public IAuthenticationSession AuthenticationSession
            {
                get;
            }
        }

        private sealed class RecordingPublisher :
            IAuthenticationEventPublisher
        {
            public string EventName { get; private set; }
            public AuthenticationStatusSnapshot Status
            {
                get;
                private set;
            }

            public Task PublishAsync(
                string eventName,
                AuthenticationStatusSnapshot status,
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
