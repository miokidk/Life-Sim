// SizeSystem.cs
// Namespace is optional—rename or remove if you like.
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LifeSim.Units
{
    /// <summary>
    /// Project-wide scale control. By default Unity is 1 unit = 1 meter.
    /// If your world uses a different scale, set UnitsPerMeter here.
    /// Place an instance in Resources as "UnitSettings" to override defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "LifeSim/Units/Unit Settings", fileName = "UnitSettings")]
    public sealed class UnitSettings : ScriptableObject
    {
        [Tooltip("How many Unity units equal 1 meter in your world. Default = 1.")]
        public float unitsPerMeter = 1f;

        private static UnitSettings _instance;
        public static UnitSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UnitSettings>("UnitSettings");
                    if (_instance == null)
                    {
                        // Safe fallback if no asset exists.
                        _instance = CreateInstance<UnitSettings>();
                        _instance.unitsPerMeter = 1f;
                    }
                }
                return _instance;
            }
        }
    }

    /// <summary>
    /// Core conversion helpers. Use these in code anywhere.
    /// Define in imperial, get Unity units out (float/Vector3).
    /// Also supports metric and formatting/parsing.
    /// </summary>
    public static class Size
    {
        // Metric factors
        public const float InchToMeter = 0.0254f;
        public const float FootToMeter = 0.3048f;
        public const float YardToMeter = 0.9144f;
        public const float MileToMeter = 1609.344f;

        static float UPM => UnitSettings.Instance?.unitsPerMeter ?? 1f; // Units Per Meter
        static float MetersToUnits(float meters) => meters * UPM;
        static float UnitsToMeters(float units) => UPM == 0f ? 0f : units / UPM;

        // ---------- Imperial → Units ----------
        public static float Inches(float inches) => MetersToUnits(inches * InchToMeter);
        public static float Feet(float feet)     => MetersToUnits(feet   * FootToMeter);
        public static float Yards(float yards)   => MetersToUnits(yards  * YardToMeter);
        public static float Miles(float miles)   => MetersToUnits(miles  * MileToMeter);

        public static float FeetInches(int feet, float inches) => Feet(feet) + Inches(inches);

        public static Vector3 Inches(float x, float y, float z)
            => new Vector3(Inches(x), Inches(y), Inches(z));

        public static Vector3 Feet(float x, float y, float z)
            => new Vector3(Feet(x), Feet(y), Feet(z));

        public static Vector3 FeetInches(
            int fx, float ix,
            int fy, float iy,
            int fz, float iz)
            => new Vector3(FeetInches(fx, ix), FeetInches(fy, iy), FeetInches(fz, iz));

        // ---------- Metric → Units (handy if you ever need it) ----------
        public static float Meters(float meters)       => MetersToUnits(meters);
        public static float Centimeters(float cm)      => MetersToUnits(cm / 100f);
        public static float Millimeters(float mm)      => MetersToUnits(mm / 1000f);

        // ---------- Units → Imperial ----------
        public static float UnitsToInches(float units) => UnitsToMeters(units) / InchToMeter;
        public static float UnitsToFeet(float units)   => UnitsToMeters(units) / FootToMeter;

        public static (int feet, float inches) UnitsToFeetInches(float units)
        {
            var totalInches = UnitsToInches(units);
            var feet = Mathf.FloorToInt(totalInches / 12f);
            var inches = totalInches - feet * 12f;
            return (feet, inches);
        }

        // ---------- Display helpers ----------
        public static string FormatFeetInches(float units, int inchDecimals = 2, bool symbols = true)
        {
            var (ft, inch) = UnitsToFeetInches(units);
            var inStr = System.Math.Round(inch, inchDecimals).ToString($"F{inchDecimals}");
            return symbols ? $"{ft}′ {inStr}″" : $"{ft} ft {inStr} in";
        }

        public static string FormatMeters(float units, int decimals = 3)
            => $"{System.Math.Round(UnitsToMeters(units), decimals)} m";

        // ---------- Parsing ----------
        // Accepts: "5'10\"", "5 ft 10.5 in", "70in", "1.78 m", "180 cm", "1800 mm"
        public static bool TryParseToUnits(string s, out float units)
        {
            units = 0f;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToLowerInvariant();

            // Metric
            if (s.EndsWith("mm") && float.TryParse(s.Replace("mm","").Trim(), out var mm))
                { units = Millimeters(mm); return true; }

            if (s.EndsWith("cm") && float.TryParse(s.Replace("cm","").Trim(), out var cm))
                { units = Centimeters(cm); return true; }

            if (s.EndsWith("m") && !s.EndsWith("mm") && !s.EndsWith("cm") &&
                float.TryParse(s.TrimEnd('m',' '), out var m))
                { units = Meters(m); return true; }

            // Inches only
            if (s.EndsWith("in") || s.EndsWith("\""))
            {
                var val = s.Replace("in","").Replace("\"","").Trim();
                if (float.TryParse(val, out var inchOnly))
                    { units = Inches(inchOnly); return true; }
            }

            // Feet & inches
            try
            {
                s = s.Replace("feet","ft").Replace("’","'").Replace("′","'").Replace("″","\"");
                var hasFtSym = s.Contains("'") || s.Contains("ft");
                if (hasFtSym)
                {
                    // feet
                    string ftPart = s.Contains("'")
                        ? s.Split('\'')[0]
                        : s.Split(new[] { "ft" }, System.StringSplitOptions.None)[0];
                    int ft = int.TryParse(ftPart.Trim(), out var f) ? f : 0;

                    // inches
                    var rest = s.Substring(ftPart.Length).Replace("ft","").Replace("'","");
                    rest = rest.Replace("in","").Replace("\"","").Trim();
                    float inch = float.TryParse(rest, out var ri) ? ri : 0f;

                    units = FeetInches(ft, inch);
                    return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }
    }

    /// <summary>
    /// Fluent sugar: 80f.Inches(), 6.Ft(), (3f,7f,0.5f).Feet()
    /// </summary>
    public static class UnitX
    {
        public static float Inches(this float v) => Size.Inches(v);
        public static float Inches(this int v)   => Size.Inches(v);
        public static float Ft(this float v)     => Size.Feet(v);
        public static float Ft(this int v)       => Size.Feet(v);
        public static float FtIn(this (int feet, float inches) v) => Size.FeetInches(v.feet, v.inches);

        public static Vector3 Inches(this (float x, float y, float z) v) => Size.Inches(v.x, v.y, v.z);
        public static Vector3 Feet(this (float x, float y, float z) v)   => Size.Feet(v.x, v.y, v.z);
    }

    /// <summary>
    /// Serializable inspector-friendly length (optional).
    /// </summary>
    [System.Serializable]
    public struct ImperialLength
    {
        public int feet;
        public float inches;
        public float ToUnits() => Size.FeetInches(feet, inches);
        public override string ToString() => $"{feet} ft {inches} in";
    }
}

#if UNITY_EDITOR
namespace LifeSim.Units
{
    [CustomPropertyDrawer(typeof(ImperialLength))]
    public class ImperialLengthDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var feetProp   = property.FindPropertyRelative("feet");
            var inchesProp = property.FindPropertyRelative("inches");

            var rect = EditorGUI.PrefixLabel(position, label);
            var half = rect.width * 0.5f;
            var r1 = new Rect(rect.x, rect.y, half - 2, rect.height);
            var r2 = new Rect(rect.x + half + 2, rect.y, half - 2, rect.height);

            feetProp.intValue      = EditorGUI.IntField(r1,  new GUIContent("ft"), feetProp.intValue);
            inchesProp.floatValue  = EditorGUI.FloatField(r2, new GUIContent("in"), inchesProp.floatValue);
            EditorGUI.EndProperty();
        }
    }
}
#endif
