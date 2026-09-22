using Keri.Mail;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="Emailer.ResolveSmtp"/> — the per-message SMTP
    /// copy that keeps a host override from leaking back into the caller's
    /// settings.
    /// </summary>
    public class SmtpResolutionTests
    {
        private static SmtpSettings Configured()
        {
            return new SmtpSettings
            {
                Host = "relay.internal.example",
                From = "reports@example.test",
                Port = 587,
                EnableSsl = true,
                Username = "svc_reports",
                Password = "not-a-real-secret",
                DeveloperEmail = "dev@example.test"
            };
        }

        // -----------------------------------------------------------------
        // The bug these exist for
        // -----------------------------------------------------------------

        [Fact]
        public void AnOverrideDoesNotMutateTheCallersSettings()
        {
            // EmailSpecs.smtpspecs held a reference to the caller's object, so
            // applying a per-message host wrote straight back into it. The
            // composition root builds one SmtpSettings and reuses it — one
            // overridden message repointed every send after it.
            var caller = Configured();

            Emailer.ResolveSmtp(caller, "other-relay.example");

            Assert.Equal("relay.internal.example", caller.Host);
        }

        [Fact]
        public void TheOverrideIsAppliedToTheCopy()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(Configured(), "other-relay.example");

            Assert.Equal("other-relay.example", resolved.Host);
        }

        [Fact]
        public void TwoMessagesOffOneSettingsObjectDoNotContaminateEachOther()
        {
            var caller = Configured();

            SmtpSettings first = Emailer.ResolveSmtp(caller, "one-off.example");
            SmtpSettings second = Emailer.ResolveSmtp(caller, null);

            Assert.Equal("one-off.example", first.Host);
            Assert.Equal("relay.internal.example", second.Host);
        }

        // -----------------------------------------------------------------
        // Copy fidelity
        // -----------------------------------------------------------------

        [Fact]
        public void TheResultIsAlwaysANewInstance()
        {
            var caller = Configured();

            Assert.NotSame(caller, Emailer.ResolveSmtp(caller, null));
        }

        [Fact]
        public void EveryFieldIsCarriedOntoTheCopy()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(Configured(), null);

            Assert.Equal("relay.internal.example", resolved.Host);
            Assert.Equal("reports@example.test", resolved.From);
            Assert.Equal(587, resolved.Port);
            Assert.True(resolved.EnableSsl);
            Assert.Equal("svc_reports", resolved.Username);
            Assert.Equal("not-a-real-secret", resolved.Password);
            Assert.Equal("dev@example.test", resolved.DeveloperEmail);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ABlankOverrideLeavesTheConfiguredHost(string hostOverride)
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(Configured(), hostOverride);

            Assert.Equal("relay.internal.example", resolved.Host);
        }

        [Fact]
        public void ANullSourceYieldsTheNeutralDefaults()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(null, null);

            Assert.Null(resolved.Host);
            Assert.Equal(25, resolved.Port);
            Assert.False(resolved.EnableSsl);
            Assert.Equal(string.Empty, resolved.Username);
        }

        [Fact]
        public void ANullSourceStillTakesAnOverride()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(null, "relay.example");

            Assert.Equal("relay.example", resolved.Host);
        }
    }
}
