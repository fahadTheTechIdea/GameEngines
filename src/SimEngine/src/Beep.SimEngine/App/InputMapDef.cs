using System;
using System.Collections.Generic;
using Beep.SimEngine.Content;

namespace Beep.SimEngine.App
{
    /// <summary>
    /// A player intent from the platform, already named: "clock.pause", "select", "build". A host maps
    /// its keys, clicks and touches to actions (through an <see cref="InputMap"/>); the application
    /// decides what an action does.
    /// </summary>
    public struct InputAction
    {
        public string Action;
        /// <summary>The tile the pointer was over, if any.</summary>
        public TileCoord Tile;
        /// <summary>The entity under the pointer, if any.</summary>
        public EntityId Target;
        /// <summary>A free argument (a def id picked from a menu, typed text).</summary>
        public string Argument;

        public InputAction(string action, string argument = null) : this()
        {
            Action = action;
            Argument = argument;
            Tile = TileCoord.Invalid;
        }

        public override string ToString() => Action + (Argument != null ? " " + Argument : "");
    }


    /// <summary>
    /// Input names ("space", "plus", "mouse.left") to actions, as content authors them (section
    /// "input_maps"): { "id": "default", "bindings": { "space": "clock.pause", "f4": "clock.speed:4" } }.
    /// A binding is an action id, optionally followed by ":" and an argument. What the names mean (which key
    /// is "space") is the host's; what the actions do is the app's.
    /// </summary>
    public sealed class InputMap
    {
        private readonly Dictionary<string, KeyValuePair<string, string>> _bindings = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);

        /// <summary>The framework's fallback for a game whose content declares no input map: the clock and quitting.</summary>
        public static InputMap Default()
        {
            var m = new InputMap();
            m.Bind("escape", "app.exit");
            m.Bind("q", "app.exit");
            return m;
        }

        public InputMap Bind(string input, string binding)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(binding)) throw new ArgumentException("a binding needs an input and an action");
            int colon = binding.IndexOf(':');
            _bindings[input] = colon < 0 ? new KeyValuePair<string, string>(binding, null) : new KeyValuePair<string, string>(binding.Substring(0, colon), binding.Substring(colon + 1));
            return this;
        }

        public int Count => _bindings.Count;

        public bool TryMap(string input, out InputAction action)
        {
            if (_bindings.TryGetValue(input ?? "", out KeyValuePair<string, string> b)) { action = new InputAction(b.Key, b.Value); return true; }
            action = default;
            return false;
        }

        /// <summary>The names bound to an action (for a help screen or a key-config UI), in name order.</summary>
        public IReadOnlyList<string> InputsFor(string action)
        {
            var names = new List<string>();
            foreach (KeyValuePair<string, KeyValuePair<string, string>> kv in _bindings) if (kv.Value.Key == action) names.Add(kv.Key);
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    /// <summary>An input map as content (section "input_maps"). Read once; <see cref="Build"/> makes the map an app uses.</summary>
    public sealed class InputMapDef : Def
    {
        private readonly List<KeyValuePair<string, string>> _bindings = new List<KeyValuePair<string, string>>();

        protected override void ReadCore(JsonReader r)
        {
            JsonValue b = r.Object("bindings") ?? throw r.Error("an input map has bindings");
            for (int i = 0; i < b.Count; i++)
            {
                string name = b.KeyAt(i);
                JsonValue v = b.ValueAt(i);
                if (v.Kind != JsonKind.String || v.Text.Length == 0 || v.Text[0] == ':') throw r.Error("binding '" + name + "' must be an action id (optionally action:argument)");
                _bindings.Add(new KeyValuePair<string, string>(name, v.Text));
            }
            if (_bindings.Count == 0) throw r.Error("an input map binds at least one input");
        }

        public InputMap Build()
        {
            var map = new InputMap();
            for (int i = 0; i < _bindings.Count; i++) map.Bind(_bindings[i].Key, _bindings[i].Value);
            return map;
        }
    }
}
