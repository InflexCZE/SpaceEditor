using System.Text;
using System.Windows;
using SpaceEditor.Rocks;

namespace SpaceEditor.Data;


// This class allows you to handle specific events on the settings class:
//  The SettingChanging event is raised before a setting's value is changed.
//  The PropertyChanged event is raised after a setting's value is changed.
//  The SettingsLoaded event is raised after the setting values are loaded.
//  The SettingsSaving event is raised before the setting values are saved.
internal sealed partial class Settings
{
    public Settings()
    {
        // // To add event handlers for saving and changing settings, uncomment the lines below:
        //
        // this.SettingChanging += this.SettingChangingEventHandler;
        //
        // this.SettingsSaving += this.SettingsSavingEventHandler;
        //
    }
    
    private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e)
    {
        // Add code to handle the SettingChangingEvent event here.
    }
    
    private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Add code to handle the SettingsSaving event here.
    }

    public Dictionary<string, string> NamedPresets
    {
        get => this.Presets.ToDictionary();
        set => this.Presets = value.ToStringCollection();
    }

    public void InvokeGameAction(Action action)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This action is about to change Game Content which may result in incompatibilities, bugs or crashes.");
        sb.AppendLine("In case of any issues use Steam Verify Game Integrity feature to automatically revert all modifications.");
        sb.AppendLine("Use at your own risk.");
        sb.AppendLine("Continue?");

        var response = MessageBox.Show
        (
            sb.ToString(),
            "Warning",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning
        );

        if (response == MessageBoxResult.OK)
        {
            action();
        }
    }
}
