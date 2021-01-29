using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using InterLayerLib;

namespace webApp
{
    public class WSNotification : WSMessage
    {
        public string notification { get; set; }

        private WSNotification()
        {
            this.type = "notification";
        }

        public WSNotification(VerificationNotificationType notificationType) : this()
        {
            switch (notificationType)
            {
                case VerificationNotificationType.verificationCanceled:
                    this.notification = "verificationCanceled";
                    break;
                case VerificationNotificationType.verificationStart:
                    this.notification = "verificationStart";
                    break;
                case VerificationNotificationType.verificationEnd:
                    this.notification = "verificationEnd";
                    break;
                case VerificationNotificationType.testCasesCanceled:
                    this.notification = "testCasesCanceled";
                    break;
                case VerificationNotificationType.testCasesStart:
                    this.notification = "testCasesStart";
                    break;
                case VerificationNotificationType.testCasesEnd:
                    this.notification = "testCasesEnd";
                    break;
                default:
                    this.notification = "unknown";
                    break;
            }
        }
    }
}