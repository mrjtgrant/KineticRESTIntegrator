using System.Collections.Generic;
using Keri.Mail;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="Emailer.emailbody"/> — the report body generated
    /// when <see cref="EmailSpecs.EmailBody"/> is not set.
    /// </summary>
    public class EmailBodyTests
    {
        private static EmailSpecs Spec()
        {
            return new EmailSpecs
            {
                EmailSubject = "Nightly export",
                EmailRecipients = new List<string> { "to@example.test" },
                EmailCCRecipients = new List<string> { "cc@example.test" },
                EmailBCCRecipients = new List<string> { "hidden-bcc@example.test" },
                EmailRecipientDefault = new List<string> { "hidden-audit@example.test" },
                FileProcessErrors = new List<string> { "Row 12: part not found" }
            };
        }

        [Fact]
        public void GeneratedBody_OmitsBlindCopyRecipients()
        {
            string body = Emailer.emailbody(Spec());

            Assert.DoesNotContain("hidden-bcc@example.test", body);
            Assert.DoesNotContain("hidden-audit@example.test", body);
            Assert.DoesNotContain("EmailBCCRecipients", body);
            Assert.DoesNotContain("EmailRecipientDefault", body);
        }

        [Fact]
        public void GeneratedBody_StillReportsErrorsAndVisibleRecipients()
        {
            string body = Emailer.emailbody(Spec());

            Assert.Contains("Row 12: part not found", body);
            Assert.Contains("FileErrorLevel", body);
            Assert.Contains("to@example.test", body);
            Assert.Contains("cc@example.test", body);
        }

        [Fact]
        public void ExplicitBody_IsUsedInsteadOfTheReport()
        {
            var spec = Spec();
            spec.EmailBody = "Line one\nLine two";

            string body = Emailer.emailbody(spec);

            Assert.Contains("Line one<br/>Line two", body);
            Assert.DoesNotContain("to@example.test", body);
        }
    }
}
