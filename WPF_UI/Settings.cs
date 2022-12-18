using System;
using System.ComponentModel;
using System.Configuration;

using ShogiEngine;

namespace WPF_UI.Properties {
    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    internal sealed partial class Settings {
        
        public Settings() {
            // // To add event handlers for saving and changing settings, uncomment the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        
        private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e) {
            // Add code to handle the SettingChangingEvent event here.
        }
        
        private void SettingsSavingEventHandler(object sender, CancelEventArgs e) {
            // Add code to handle the SettingsSaving event here.
        }
    }

    internal sealed class NetworkGameStateConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string);

        public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value)
        {
            if (value is string && !(value is null))
            {
                string[] parts = ((string)value).Split(new char[] { ',' });

                Guid.TryParse(parts[0], out var gameId);
                Guid.TryParse(parts[1], out var playerId);
                Enum.TryParse<PlayerColor>(parts[2], true, out var myColor);
                return new NetworkGameState(gameId, playerId, myColor);
            }
            return base.ConvertFrom(context, culture, value ?? throw new NullReferenceException());
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && !(value is null))
            {
                var ngs = value as NetworkGameState ?? throw new NullReferenceException();
                return string.Format("{0},{1},{2}", ngs.GameId, ngs.PlayerId, ngs.MyColor);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    [TypeConverter(typeof(NetworkGameStateConverter))]
    [System.Configuration.SettingsSerializeAs(SettingsSerializeAs.String)]
    internal sealed class NetworkGameState
    {
        public Guid GameId { get; } = Guid.Empty;
        public Guid PlayerId { get; } = Guid.Empty;
        public PlayerColor MyColor { get; }

        public NetworkGameState() => MyColor = PlayerColor.Black;

        public NetworkGameState(Guid gameId, Guid playerId, PlayerColor myColor) =>
            (GameId, PlayerId, MyColor) = (gameId, playerId, myColor);
    }
}
