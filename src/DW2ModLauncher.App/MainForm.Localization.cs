namespace DW2ModLauncherBeta
{
    public partial class MainForm
    {
        // A UI affordance, not translatable content - kept out of the
        // language files so it isn't duplicated across every "value + this
        // cell is a dropdown" resource string.
        private const string DropdownIndicator = " ▼";

        private void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.Text = text;
        }

        private string T(string key, params object[] args)
        {
            string template = Localization.Get(settings != null ? settings.Language : "en", key);
            return args != null && args.Length > 0 ? string.Format(template, args) : template;
        }

        // "label + colon + value" is generic formatting, not translatable
        // content, so it lives here once instead of being retyped (and
        // baked into the language files) at every call site that needs it.
        private string Labeled(string key, string value)
        {
            return T(key) + ": " + value;
        }
    }
}
