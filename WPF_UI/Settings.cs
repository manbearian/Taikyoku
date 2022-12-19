using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Text;
using ShogiEngine;

namespace WPF_UI.Properties
{


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

        private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e)
        {
            // Add code to handle the SettingChangingEvent event here.
        }

        private void SettingsSavingEventHandler(object sender, CancelEventArgs e)
        {
            // Add code to handle the SettingsSaving event here.
        }
    }

    internal sealed class NetworkGameStateConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string);

        public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value)
        {
            if (value is string valueAsString)
            {
                string[] parts = valueAsString.Split(new char[] { ',' });

                if (parts.Length != 3)
                    return null;
                if (!Guid.TryParse(parts[0], out var gameId))
                    return null;
                if (!Guid.TryParse(parts[1], out var playerId))
                    return null;
                if (!Enum.TryParse<PlayerColor>(parts[2], true, out var myColor))
                    return null;
                return new NetworkGameState(gameId, playerId, myColor);
            }
            return base.ConvertFrom(context, culture, value ?? throw new NullReferenceException());
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is NetworkGameState ngs)
            {
                return string.Format("{0},{1},{2}", ngs.GameId, ngs.PlayerId, ngs.MyColor);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    internal sealed class NetworkGameStateListConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string);

        public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value)
        {
            if (value is string valueAsString)
            {
                string[] parts = valueAsString.Split(new char[] { ';' });

                List<NetworkGameState> list = new List<NetworkGameState>();
                foreach (string part in parts)
                {
                    // serialziation has a trailing ';'
                    if (part.Length == 0)
                        continue;
                    var elemConverter = new NetworkGameStateConverter();
                    var elem = elemConverter.ConvertFrom(context, culture, part) as NetworkGameState ?? throw new InvalidCastException();
                    if (elem is NetworkGameState ngs)
                        list.Add(ngs);
                }
                return new NetworkGameStateList(list);
            }
            return base.ConvertFrom(context, culture, value ?? throw new NullReferenceException());
        }

        public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is NetworkGameStateList ngsList)
            {
                var elemConverter = new NetworkGameStateConverter();
                StringBuilder s = new StringBuilder();
                foreach (var ngs in ngsList.NetworkGameStates)
                {
                    s.Append(elemConverter.ConvertToString(context, culture, ngs));
                    s.Append(';');
                }
                return s.ToString();
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    [TypeConverter(typeof(NetworkGameStateConverter))]
    [SettingsSerializeAs(SettingsSerializeAs.String)]
    internal sealed class NetworkGameState : IEquatable<NetworkGameState>
    {
        public Guid GameId { get; } = Guid.Empty;
        public Guid PlayerId { get; } = Guid.Empty;
        public PlayerColor MyColor { get; }

        public bool Equals(NetworkGameState? other) => (GameId, PlayerId, MyColor) == (other?.GameId, other?.PlayerId, other?.MyColor);
        public override bool Equals(object? obj) => Equals(obj as NetworkGameState);
        public override int GetHashCode() => (GameId, PlayerId, MyColor).GetHashCode();

        public static bool operator ==(NetworkGameState lhs, NetworkGameState rhs) => lhs?.Equals(rhs) ?? lhs is null && rhs is null;
        public static bool operator !=(NetworkGameState lhs, NetworkGameState rhs) => !(lhs == rhs);

        public NetworkGameState() => MyColor = PlayerColor.Black;

        public NetworkGameState(Guid gameId, Guid playerId, PlayerColor myColor) =>
            (GameId, PlayerId, MyColor) = (gameId, playerId, myColor);
    }

    [TypeConverter(typeof(NetworkGameStateListConverter))]
    [SettingsSerializeAs(SettingsSerializeAs.String)]
    internal sealed class NetworkGameStateList
    {
        public IEnumerable<NetworkGameState> NetworkGameStates { get; } = Array.Empty<NetworkGameState>();

        public NetworkGameStateList() { }
        public NetworkGameStateList(IEnumerable<NetworkGameState> networkGameStates) => NetworkGameStates = networkGameStates;
    }
}
