using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterLayerLib
{
    public enum VerificationNotificationType
    {
        verificationCanceled,
        verificationStart,
        verificationEnd,
        testCasesCanceled,
        testCasesStart,
        testCasesEnd
    }
    public class CheckerVerificationNotification : CheckerMessage
    {
        public VerificationNotificationType notificationType { get; }

        public CheckerVerificationNotification(VerificationNotificationType notificationType)
        {
            this._type = CheckerMessageType.CheckerVerificationNotification;
            this.notificationType = notificationType;
        }
    }
}