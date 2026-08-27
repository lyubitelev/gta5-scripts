using GTA.UI;

namespace gta.Core
{
    internal static class Notifier
    {
        public static void Show(string message)
        {
            Notification.PostTicker(message, false, false);
        }
    }
}
