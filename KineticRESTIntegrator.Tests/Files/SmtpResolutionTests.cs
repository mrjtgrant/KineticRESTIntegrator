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
                host = "relay.internal.example",
                from = "reports@example.test",
                port = 587,
                enableSsl = true,
                username = "svc_reports",
                password = "not-a-real-secret",
                developerEmail = "dev@example.test"
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

            Assert.Equal("relay.internal.example", caller.host);
        }

        [Fact]
        public void TheOverrideIsAppliedToTheCopy()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(Configured(), "other-relay.example");

            Assert.Equal("other-relay.example", resolved.host);
        }

        [Fact]
        public void TwoMessagesOffOneSettingsObjectDoNotContaminateEachOther()
        {
            var caller = Configured();

            SmtpSettings first = Emailer.ResolveSmtp(caller, "one-off.example");
            SmtpSettings second = Emailer.ResolveSmtp(caller, null);

            Assert.Equal("one-off.example", first.host);
            Assert.Equal("relay.internal.example", second.host);
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

            Assert.Equal("relay.internal.example", resolved.host);
            Assert.Equal("reports@example.test", resolved.from);
            Assert.Equal(587, resolved.port);
            Assert.True(resolved.enableSsl);
            Assert.Equal("svc_reports", resolved.username);
            Assert.Equal("not-a-real-secret", resolved.password);
            Assert.Equal("dev@example.test", resolved.developerEmail);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ABlankOverrideLeavesTheConfiguredHost(string hostOverride)
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(Configured(), hostOverride);

            Assert.Equal("relay.internal.example", resolved.host);
        }

        [Fact]
        public void ANullSourceYieldsTheNeutralDefaults()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(null, null);

            Assert.Null(resolved.host);
            Assert.Equal(25, resolved.port);
            Assert.False(resolved.enableSsl);
            Assert.Equal(string.Empty, resolved.username);
        }

        [Fact]
        public void ANullSourceStillTakesAnOverride()
        {
            SmtpSettings resolved = Emailer.ResolveSmtp(null, "relay.example");

            Assert.Equal("relay.example", resolved.host);
        }
    }
}
