using UnityEngine;
using System;

public static class StatLimits
{
    /* ────────────── Core Math Functions ────────────── */

    public static int AgeScaledValue(int age, float peakAge, float declineStartAge,
                                    float maxValue, float baseValue = 5, float rateOfChange = 1.5f)
    {
        float value;
        if (age < peakAge)
        {
            value = baseValue + (maxValue - baseValue) * (age / peakAge);
        }
        else if (age < declineStartAge)
        {
            value = maxValue;
        }
        else
        {
            float declineFactor = (age - declineStartAge) * rateOfChange;
            value = maxValue - declineFactor;
        }
        return Mathf.Clamp(Mathf.RoundToInt(value + UnityEngine.Random.Range(-10f, 10f)), 0, 100);
    }

    // Deterministic version for calculating the center of a range
    public static int AgeScaledValueDeterministic(int age, float peakAge, float declineStartAge,
                                                float maxValue, float baseValue = 5, float rateOfChange = 1.5f, int absoluteMax = 100)
    {
        float value;
        if (age < peakAge)
        {
            value = baseValue + (maxValue - baseValue) * (age / peakAge);
        }
        else if (age < declineStartAge)
        {
            value = maxValue;
        }
        else
        {
            float declineFactor = (age - declineStartAge) * rateOfChange;
            value = maxValue - declineFactor;
        }
        return Mathf.Clamp(Mathf.RoundToInt(value), 0, absoluteMax);
    }

    
    public static (int min, int max) GetAgeScaledRange(int age, float peakAge, float declineStartAge,
                                                      float maxValue, float baseValue, float rateOfChange, int rangeWidth, int absoluteMax = 100)
    {
        // Calculate the central point of the range
        int centerValue = AgeScaledValueDeterministic(age, peakAge, declineStartAge, maxValue, baseValue, rateOfChange, absoluteMax);

        // Calculate min and max based on the width
        int halfWidth = Mathf.RoundToInt(rangeWidth / 2f);
        int min = centerValue - halfWidth;
        int max = centerValue + halfWidth;

        // Ensure the final range is within the absolute bounds
        return (Mathf.Clamp(min, 0, absoluteMax), Mathf.Clamp(max, 0, absoluteMax));
    }

    /// <summary>
    /// Helper function to get the relative position of a value within a range (0-1)
    /// </summary>
    private static float GetRelativePosition(float value, float min, float max)
    {
        if (max <= min) return 0.5f;
        return Mathf.Clamp01((value - min) / (max - min));
    }

    /// <summary>
    /// Helper function to apply a relative position to a new range
    /// </summary>
    private static float ApplyRelativePosition(float relativePos, float newMin, float newMax)
    {
        return newMin + relativePos * (newMax - newMin);
    }


    /* ────────────── Get Limit Functions (Unaffected) ────────────── */
    public static void GetHeightLimits(int ageYears, characterStats.SexType sex, out float minCm, out float maxCm)
    {
        // 👶 Infants & Toddlers (0-2 years) - Ranges refined based on CDC growth data (approx. 3rd-97th percentile)
        if (ageYears == 0)      // Represents 0-12 months old
        {
            minCm = 45f; // Min height at birth
            maxCm = sex == characterStats.SexType.Female ? 78f : 80f; // Max height at 12 months
        }
        else if (ageYears == 1) // Represents 12-24 months old
        {
            minCm = sex == characterStats.SexType.Female ? 68f : 70f; // Min height at 12 months
            maxCm = sex == characterStats.SexType.Female ? 91f : 92f; // Max height at 24 months
        }
        else if (ageYears == 2) // Represents 2-3 years old
        {
            minCm = sex == characterStats.SexType.Female ? 80f : 81f; // Min height at 2 yrs
            maxCm = sex == characterStats.SexType.Female ? 99f : 100f; // Max height at 3 yrs
        }

        // 👧 Children (3-8 years) - Ranges refined based on CDC growth data
        else if (ageYears == 3) // Represents 3-4 years old
        {
            minCm = sex == characterStats.SexType.Female ? 87f : 88f;
            maxCm = sex == characterStats.SexType.Female ? 108f : 109f;
        }
        else if (ageYears <= 5) // Represents 4-5 years old
        {
            minCm = sex == characterStats.SexType.Female ? 93f : 94f;
            maxCm = sex == characterStats.SexType.Female ? 117f : 118f;
        }
        else if (ageYears <= 8) // Represents 6-8 years old
        {
            minCm = sex == characterStats.SexType.Female ? 105f : 106f;
            maxCm = sex == characterStats.SexType.Female ? 135f : 136f;
        }
        
        // 🧑 Tweens & Teens (9-19 years) - Using previously established inclusive floor
        else if (ageYears <= 12) { minCm = 120f;                                     maxCm = sex == characterStats.SexType.Female ? 165f : 160f; }
        else if (ageYears <= 16) { minCm = sex == characterStats.SexType.Female ? 120f : 125f; maxCm = sex == characterStats.SexType.Female ? 175f : 185f; }
        else if (ageYears <= 19) { minCm = sex == characterStats.SexType.Female ? 120f : 130f; maxCm = sex == characterStats.SexType.Female ? 195f : 210f; }

        // 👨 Adults (20-69 years) - Using previously established inclusive floor
        else if (ageYears <= 69)
        {
            switch (sex)
            {
                case characterStats.SexType.Male:   minCm = 130f; maxCm = 230f; break;
                case characterStats.SexType.Female: minCm = 120f; maxCm = 215f; break;
                default:                            minCm = 125f; maxCm = 222f; break;
            }
        }
        
        // 🧓 Seniors (70+ years) - Accounting for potential height loss
        else
        {
            switch (sex)
            {
                case characterStats.SexType.Male:   minCm = 125f; maxCm = 220f; break;
                case characterStats.SexType.Female: minCm = 115f; maxCm = 205f; break;
                default:                            minCm = 120f; maxCm = 212f; break;
            }
        }
    }
    public static void GetBMILimits(int ageYears, characterStats.SexType sex, out float minBMI, out float maxBMI) { 
        if (ageYears < 2) { minBMI = 14f; maxBMI = 19f; }
        else if (ageYears < 20) { minBMI = 15f; maxBMI = 27f; }
        // Increased max BMI to allow for higher fat levels.
        else { switch (sex) { case characterStats.SexType.Male: minBMI = 19f; maxBMI = 70f; break; case characterStats.SexType.Female: minBMI = 18f; maxBMI = 75f; break; default: minBMI = 18.5f; maxBMI = 72f; break; } }
    }
    public static void GetFootSizeLimits(int ageYears, characterStats.SexType sex, out int minSize, out int maxSize)
    {
        // Shoe size indices 0-57, representing a continuous scale from baby to large adult.

        // Infants & Toddlers (0-3 years)
        if (ageYears == 0)      { minSize = 0; maxSize = 12; } // 0C - 6C
        else if (ageYears == 1) { minSize = 4; maxSize = 16; } // 2C - 8C
        else if (ageYears <= 3) { minSize = 10; maxSize = 24; } // 5C - 12C

        // Children (4-8 years)
        else if (ageYears <= 5) { minSize = 16; maxSize = 30; } // 8C - 1.5Y
        else if (ageYears <= 8) { minSize = 22; maxSize = 36; } // 11C - 5Y

        // Tweens & Teens (9-17 years) - Sex differences become more pronounced
        else if (ageYears <= 12)
        {
            minSize = 30; // 1.5Y
            maxSize = sex == characterStats.SexType.Male ? 44 : 42; // up to ~9M for boys, ~8M for girls
        }
        else if (ageYears <= 17)
        {
            minSize = sex == characterStats.SexType.Male ? 38 : 36; // ~6Y for boys, ~5Y for girls
            maxSize = sex == characterStats.SexType.Male ? 57 : 51; // up to 15M for boys, ~12M for girls
        }
        
        // Adults (18+)
        else
        {
            switch (sex)
            {
                case characterStats.SexType.Male:
                    minSize = 39; // 6.5Y/M
                    maxSize = 57; // 15M
                    break;
                case characterStats.SexType.Female:
                    minSize = 36; // 5Y/W7
                    maxSize = 53; // 13.5M/W15
                    break;
                default: // Intersex
                    minSize = 37; // 5.5Y
                    maxSize = 57; // 15M
                    break;
            }
        }
    }
    public static float SampleHeightCm(int ageYears, characterStats.SexType sex)
    {
        float minCm, maxCm; GetHeightLimits(ageYears, sex, out minCm, out maxCm);
        float range = Mathf.Max(0.01f, maxCm - minCm);

        // Aim near typical adult medians when applicable, else midpoint
        float target = (minCm + maxCm) * 0.5f;
        if (ageYears >= 17 && ageYears <= 50)
        {
            float typical = (sex == characterStats.SexType.Male) ? 176f : 163f;
            if (typical >= minCm && typical <= maxCm) target = typical;
        }

        // Irwin–Hall (sum of 3 uniforms) ≈ bell around 0.5
        float t = (UnityEngine.Random.value + UnityEngine.Random.value + UnityEngine.Random.value) / 3f;
        float targetT = (target - minCm) / range;

        // Pull the sample halfway toward the target
        t = Mathf.Lerp(t, targetT, 0.5f);

        return minCm + t * range;
    }

    // ── Random BMI (uses existing limits; bell-ish toward realistic center) ───
    public static float SampleBMI(int ageYears, characterStats.SexType sex)
    {
        float minBMI, maxBMI; GetBMILimits(ageYears, sex, out minBMI, out maxBMI);
        float range = Mathf.Max(0.01f, maxBMI - minBMI);

        // Choose a reasonable target within limits
        float target;
        if (ageYears < 2)         target = Mathf.Clamp(17f,   minBMI, maxBMI);
        else if (ageYears < 20)   target = Mathf.Clamp(18.5f, minBMI, maxBMI);
        else
        {
            float typical = (sex == characterStats.SexType.Male) ? 26f : 25f;
            target = Mathf.Clamp(typical, minBMI, maxBMI);
        }

        float t = (UnityEngine.Random.value + UnityEngine.Random.value + UnityEngine.Random.value) / 3f;
        float targetT = (target - minBMI) / range;
        t = Mathf.Lerp(t, targetT, 0.5f);

        return minBMI + t * range;
    }
    public static void CmToFtIn(float cm, out int ft, out int inches) { float totalIn = cm / 2.54f; ft = Mathf.FloorToInt(totalIn / 12f); inches = Mathf.Clamp(Mathf.RoundToInt(totalIn - ft * 12f), 0, 11); }
    public static int SampleWeightedAge()
    {
        float r = UnityEngine.Random.value;
        if (r < 0.01f)  return UnityEngine.Random.Range(0, 2);     // 0–1
        if (r < 0.03f)  return UnityEngine.Random.Range(2, 5);     // 2–4
        if (r < 0.13f)  return UnityEngine.Random.Range(5, 13);    // 5–12
        if (r < 0.23f)  return UnityEngine.Random.Range(13, 20);   // 13–19
        if (r < 0.53f)  return UnityEngine.Random.Range(20, 40);   // 20–39
        if (r < 0.78f)  return UnityEngine.Random.Range(40, 60);   // 40–59
        if (r < 0.96f)  return UnityEngine.Random.Range(60, 81);   // 60–80
        return UnityEngine.Random.Range(81, 111);                   // 81–110 (maxExclusive)
    }

    // Store previous ranges for relative positioning
    private static (float min, float max) previousHeightRange = (-1, -1);
    private static (float min, float max) previousWeightRange = (-1, -1);
    private static System.Collections.Generic.Dictionary<string, (int min, int max)> previousRanges = 
        new System.Collections.Generic.Dictionary<string, (int min, int max)>();

    /* ────────────── GROUP: Physique Clamping ────────────── */

    public static void ClampHeight(characterStats stats) { 
        float minCm, maxCm; 
        GetHeightLimits(stats.profile.age.years, stats.profile.sex, out minCm, out maxCm);
        float currentCm = stats.profile.height.feet * 30.48f + stats.profile.height.inches * 2.54f;
        
        // Check if we have a previous range stored
        if (previousHeightRange.min >= 0 && previousHeightRange.max > previousHeightRange.min)
        {
            // Calculate relative position in the old range
            float relativePos = GetRelativePosition(currentCm, previousHeightRange.min, previousHeightRange.max);
            // Apply the relative position to the new range
            currentCm = ApplyRelativePosition(relativePos, minCm, maxCm);
        }
        
        float clampedCm = Mathf.Clamp(currentCm, minCm, maxCm);
        CmToFtIn(clampedCm, out int ft, out int inches); 
        stats.profile.height.feet = ft; 
        stats.profile.height.inches = inches;
        
        // Store the current range for next time
        previousHeightRange = (minCm, maxCm);
    }
    
    public static void ClampWeight(characterStats stats) { 
        if (stats?.profile == null) return;
        stats.profile.weight ??= new characterStats.WeightStats();

        float minBMI, maxBMI; 
        GetBMILimits(stats.profile.age.years, stats.profile.sex, out minBMI, out maxBMI);
        float heightCm = stats.profile.height.feet * 30.48f + stats.profile.height.inches * 2.54f;
        float heightM = heightCm / 100f;
        int minWeight = Mathf.RoundToInt(minBMI * Mathf.Pow(heightM, 2f) * 2.20462f);
        int maxWeight = Mathf.RoundToInt(maxBMI * Mathf.Pow(heightM, 2f) * 2.20462f);
        
        int currentWeight = stats.profile.weight.pounds;
        
        // Check if we have a previous range stored
        if (previousWeightRange.min >= 0 && previousWeightRange.max > previousWeightRange.min)
        {
            // Calculate relative position in the old range
            float relativePos = GetRelativePosition(currentWeight, previousWeightRange.min, previousWeightRange.max);
            // Apply the relative position to the new range
            currentWeight = Mathf.RoundToInt(ApplyRelativePosition(relativePos, minWeight, maxWeight));
        }
        
        stats.profile.weight.pounds = Mathf.Clamp(currentWeight, minWeight, maxWeight);
        
        // Store the current range for next time
        previousWeightRange = (minWeight, maxWeight);
    }
    
    public static void ClampFootSize(characterStats stats)
    {
        if (stats?.legs?.left?.foot == null || stats?.legs?.right?.foot == null) return;

        GetFootSizeLimits(stats.profile.age.years, stats.profile.sex, out int minSize, out int maxSize);

        // Use the left foot's size as the master value
        int currentSize = stats.legs.left.foot.size;
        
        string key = "footSize";
        // Use the ClampRelative helper to preserve the percentile
        int newSize = ClampRelative(key, currentSize, minSize, maxSize);
        newSize = Mathf.Clamp(newSize, minSize, maxSize);

        // Apply the same clamped size to both feet
        stats.legs.left.foot.size = newSize;
        stats.legs.right.foot.size = newSize;
    }

    public static void ClampStrength(characterStats stats)
    {
        if (stats == null || stats.profile == null) return;

        // 0–200 range
        (int min, int max) = GetAgeScaledRange(stats.profile.age.years, 35, 55, 195, 2, 1.0f, 60, 200);
        min = 0; max = Mathf.Max(min + 1, max);

        const string key = "strength";
        bool hasPrev = previousRanges.TryGetValue(key, out var prev);

        int ClampOne(int v)
        {
            if (hasPrev && prev.max > prev.min)
            {
                float rel = (prev.max <= prev.min) ? 0.5f : Mathf.Clamp01((v - prev.min) / (float)(prev.max - prev.min));
                v = Mathf.RoundToInt(min + rel * (max - min));
            }
            return Mathf.Clamp(v, min, max);
        }

        // Arms
        if (stats.arms != null)
        {
            if (stats.arms.left  != null)  stats.arms.left.strength  = ClampOne(stats.arms.left.strength);
            if (stats.arms.right != null)  stats.arms.right.strength = ClampOne(stats.arms.right.strength);
        }

        // Legs
        if (stats.legs != null)
        {
            if (stats.legs.left  != null)  stats.legs.left.strength  = ClampOne(stats.legs.left.strength);
            if (stats.legs.right != null)  stats.legs.right.strength = ClampOne(stats.legs.right.strength);
        }

        // Torso / Chest / Jaw
        if (stats.torso != null)
        {
            stats.torso.strength = ClampOne(stats.torso.strength);
            if (stats.torso.chest != null)
                stats.torso.chest.strength = ClampOne(stats.torso.chest.strength);
        }
        if (stats.head?.jaw != null)
            stats.head.jaw.strength = ClampOne(stats.head.jaw.strength);

        previousRanges[key] = (min, max);
    }

    
    public static void ClampBodyComposition(characterStats stats)
    {
        if (stats == null) return;

        // ---------- Safe anthropometrics ----------
        var prof = stats.profile;
        int feet   = prof != null ? prof.height.feet   : 0;
        int inches = prof != null ? prof.height.inches : 0;
        float heightM = ((feet * 12f + inches) * 2.54f) / 100f;

        float pounds   = (prof != null && prof.weight != null) ? prof.weight.pounds : 150f;
        float weightKg = pounds / 2.20462f;
        float bmi      = heightM > 0.01f ? weightKg / (heightM * heightM) : 22f;

        // ---------- Targets ----------
        int targetFat = Mathf.RoundToInt(bmi * 1.1f);
        const int fatSpread = 15;

        int la = stats.arms?.left?.strength   ?? 50;
        int ra = stats.arms?.right?.strength  ?? 50;
        int ll = stats.legs?.left?.strength   ?? 50;
        int rl = stats.legs?.right?.strength  ?? 50;
        int ts = stats.torso?.strength        ?? 50;

        float avgStrength = (la + ra + ll + rl + ts) / 5f;
        int targetMuscle = Mathf.RoundToInt(avgStrength - 10);
        const int muscleSpread = 20;

        int ClampVal(int v, int min, int max) => Mathf.Clamp(v, Mathf.Max(0, min), Mathf.Min(200, max));

        // ---------- Arms ----------
        if (stats.arms != null)
        {
            if (stats.arms.left != null)
            {
                int minFat = targetFat - fatSpread, maxFat = targetFat + fatSpread;
                int minMus = targetMuscle - muscleSpread, maxMus = targetMuscle + muscleSpread;
                stats.arms.left.fat         = ClampVal(stats.arms.left.fat,         minFat, maxFat);
                stats.arms.left.muscle_mass = ClampVal(stats.arms.left.muscle_mass, minMus, maxMus);
            }
            if (stats.arms.right != null)
            {
                int minFat = targetFat - fatSpread, maxFat = targetFat + fatSpread;
                int minMus = targetMuscle - muscleSpread, maxMus = targetMuscle + muscleSpread;
                stats.arms.right.fat         = ClampVal(stats.arms.right.fat,         minFat, maxFat);
                stats.arms.right.muscle_mass = ClampVal(stats.arms.right.muscle_mass, minMus, maxMus);
            }
        }

        // ---------- Legs (slightly more muscle bias) ----------
        if (stats.legs != null)
        {
            if (stats.legs.left != null)
            {
                int minFat = targetFat - fatSpread, maxFat = targetFat + fatSpread;
                int minMus = (targetMuscle + 10) - muscleSpread, maxMus = (targetMuscle + 10) + muscleSpread;
                stats.legs.left.fat         = ClampVal(stats.legs.left.fat,         minFat, maxFat);
                stats.legs.left.muscle_mass = ClampVal(stats.legs.left.muscle_mass, minMus, maxMus);
            }
            if (stats.legs.right != null)
            {
                int minFat = targetFat - fatSpread, maxFat = targetFat + fatSpread;
                int minMus = (targetMuscle + 10) - muscleSpread, maxMus = (targetMuscle + 10) + muscleSpread;
                stats.legs.right.fat         = ClampVal(stats.legs.right.fat,         minFat, maxFat);
                stats.legs.right.muscle_mass = ClampVal(stats.legs.right.muscle_mass, minMus, maxMus);
            }
        }

        // ---------- Torso + components ----------
        if (stats.torso != null)
        {
            int minFat = targetFat - fatSpread, maxFat = targetFat + fatSpread;
            int minMus = targetMuscle - muscleSpread, maxMus = targetMuscle + muscleSpread;

            stats.torso.fat         = ClampVal(stats.torso.fat,         minFat, maxFat);
            stats.torso.muscle_mass = ClampVal(stats.torso.muscle_mass, minMus, maxMus);

            if (stats.torso.chest != null)
            {
                stats.torso.chest.fat         = ClampVal(stats.torso.chest.fat,         minFat, maxFat);
                stats.torso.chest.muscle_mass = ClampVal(stats.torso.chest.muscle_mass, minMus, maxMus);
            }
            if (stats.torso.lower_back != null)
            {
                int lbMinFat = (targetFat - 5) - fatSpread, lbMaxFat = (targetFat - 5) + fatSpread;
                stats.torso.lower_back.fat         = ClampVal(stats.torso.lower_back.fat,         lbMinFat, lbMaxFat);
                stats.torso.lower_back.muscle_mass = ClampVal(stats.torso.lower_back.muscle_mass, minMus, maxMus);
            }
            if (stats.torso.abdomen != null)
            {
                int abMinFat = (targetFat + 5) - fatSpread, abMaxFat = (targetFat + 5) + fatSpread;
                int abMinMus = (targetMuscle - 10) - muscleSpread, abMaxMus = (targetMuscle - 10) + muscleSpread;
                stats.torso.abdomen.fat         = ClampVal(stats.torso.abdomen.fat,         abMinFat, abMaxFat);
                stats.torso.abdomen.muscle_mass = ClampVal(stats.torso.abdomen.muscle_mass, abMinMus, abMaxMus);
            }
        }

        // ---------- Neck (usually less of both) ----------
        if (stats.neck != null)
        {
            int minFat = (targetFat - 10) - fatSpread, maxFat = (targetFat - 10) + fatSpread;
            int minMus = (targetMuscle - 15) - muscleSpread, maxMus = (targetMuscle - 15) + muscleSpread;
            stats.neck.fat         = ClampVal(stats.neck.fat,         minFat, maxFat);
            stats.neck.muscle_mass = ClampVal(stats.neck.muscle_mass, minMus, maxMus);
        }
    }


    public static void ClampFlexibilityAndFatigue(characterStats stats)
    {
        int ageYears = stats.profile.age.years;

        // --- CORRECTED LOGIC FOR LIMB FLEXIBILITY ---
        // Instead of a narrow band, we now define the range from 0 up to an age-appropriate maximum.
        const int flexMin = 0; 
        int flexMax = AgeScaledValueDeterministic(ageYears, 18, 40, 145, 20, 1.2f, 150);
        
        string flexKey = "flexibility";
        bool hasFlexPrevious = previousRanges.TryGetValue(flexKey, out var prevFlexRange);

        const int stiffnessSpread = 15; // Allows stiffness to vary from the inverse of flexibility

        // --- Process Arms ---
        foreach (var arm in new[] { stats.arms.left, stats.arms.right })
        {
            if (arm == null) continue;

            // 1. Clamp Flexibility first based on the new [0, max] range
            int flexibility = arm.flexibility;
            if (hasFlexPrevious && prevFlexRange.max > prevFlexRange.min)
            {
                float relPos = GetRelativePosition(flexibility, prevFlexRange.min, prevFlexRange.max);
                flexibility = Mathf.RoundToInt(ApplyRelativePosition(relPos, flexMin, flexMax));
            }
            arm.flexibility = Mathf.Clamp(flexibility, flexMin, flexMax);

            // 2. Define a *target* for stiffness and fatigue based on the now-clamped flexibility
            int targetStiffness = 100 - arm.flexibility;
            int targetFatigue = 100 - arm.flexibility; // Assuming fatigue is also related

            // 3. Clamp stiffness and fatigue in a range AROUND their targets
            arm.stiffness = Mathf.Clamp(arm.stiffness, targetStiffness - stiffnessSpread, targetStiffness + stiffnessSpread);
            arm.fatigue = Mathf.Clamp(arm.fatigue, targetFatigue - stiffnessSpread, targetFatigue + stiffnessSpread);
        }

        // --- Process Legs (using the same logic) ---
        foreach (var leg in new[] { stats.legs.left, stats.legs.right })
        {
            if (leg == null) continue;

            int flexibility = leg.flexibility;
            if (hasFlexPrevious && prevFlexRange.max > prevFlexRange.min)
            {
                float relPos = GetRelativePosition(flexibility, prevFlexRange.min, prevFlexRange.max);
                flexibility = Mathf.RoundToInt(ApplyRelativePosition(relPos, flexMin, flexMax));
            }
            leg.flexibility = Mathf.Clamp(flexibility, flexMin, flexMax);

            int targetStiffness = 100 - leg.flexibility;
            int targetFatigue = 100 - leg.flexibility;

            leg.stiffness = Mathf.Clamp(leg.stiffness, targetStiffness - stiffnessSpread, targetStiffness + stiffnessSpread);
            leg.fatigue = Mathf.Clamp(leg.fatigue, targetFatigue - stiffnessSpread, targetFatigue + stiffnessSpread);
        }

        previousRanges[flexKey] = (flexMin, flexMax);

        // --- Apply same logic to Torso ---
        const int torsoFlexMin = 0;
        int torsoFlexMax = AgeScaledValueDeterministic(ageYears, 20, 45, 140, 20, 1.2f, 150);
        
        string torsoFlexKey = "torsoFlexibility";
        bool hasTorsoPrevious = previousRanges.TryGetValue(torsoFlexKey, out var prevTorsoRange);
        
        int torsoFlex = stats.torso.flexibility;
        if (hasTorsoPrevious && prevTorsoRange.max > prevTorsoRange.min)
        {
            float relPos = GetRelativePosition(torsoFlex, prevTorsoRange.min, prevTorsoRange.max);
            torsoFlex = Mathf.RoundToInt(ApplyRelativePosition(relPos, torsoFlexMin, torsoFlexMax));
        }
        stats.torso.flexibility = Mathf.Clamp(torsoFlex, torsoFlexMin, torsoFlexMax);
        
        int targetTorsoStiffness = 100 - stats.torso.flexibility;
        stats.torso.stiffness = Mathf.Clamp(stats.torso.stiffness, targetTorsoStiffness - stiffnessSpread, targetTorsoStiffness + stiffnessSpread);
        previousRanges[torsoFlexKey] = (torsoFlexMin, torsoFlexMax);

        // --- Torso Fatigue (Separate from Flexibility) ---
        (int fatigueMin, int fatigueMax) = GetAgeScaledRange(ageYears, 60, 80, 95, 5, -0.7f, 30);
        string fatigueKey = "torsoFatigue";
        bool hasFatiguePrevious = previousRanges.TryGetValue(fatigueKey, out var prevFatigueRange);
        
        int torsoFatigue = stats.torso.fatigue;
        if (hasFatiguePrevious && prevFatigueRange.max > prevFatigueRange.min)
        {
            float relPos = GetRelativePosition(torsoFatigue, prevFatigueRange.min, prevFatigueRange.max);
            torsoFatigue = Mathf.RoundToInt(ApplyRelativePosition(relPos, fatigueMin, fatigueMax));
        }
        stats.torso.fatigue = Mathf.Clamp(torsoFatigue, fatigueMin, fatigueMax);
        previousRanges[fatigueKey] = (fatigueMin, fatigueMax);
    }


    /* ────────────── GROUP: Health & Senses Clamping ────────────── */

    public static void ClampSenses(characterStats stats)
    {

        stats.senses         ??= new characterStats.SensesStats();
        stats.senses.sight   ??= new characterStats.SightStats();
        stats.senses.hearing ??= new characterStats.HearingStats();
        stats.senses.taste   ??= new characterStats.TasteStats();
        stats.senses.touch   ??= new characterStats.TouchStats();

        int age = stats.profile.age.years;

        var (_, vaMax) = GetAgeScaledRange(age, 8, 40, 100, 10, 1.1f, 40);
        string vaKey = "visualAcuity";
        bool hasVaPrevious = previousRanges.TryGetValue(vaKey, out var prevVaRange);

        int visualAcuity = stats.senses.sight.visual_acuity;
        if (hasVaPrevious && prevVaRange.max > prevVaRange.min)
        {
            float relPos = GetRelativePosition(visualAcuity, prevVaRange.min, prevVaRange.max);
            visualAcuity = Mathf.RoundToInt(ApplyRelativePosition(relPos, 0, vaMax));
        }
        // Clamp from 0 to the calculated max.
        stats.senses.sight.visual_acuity = Mathf.Clamp(visualAcuity, 0, vaMax);
        previousRanges[vaKey] = (0, vaMax);

        // Applied the same logic for color perception.
        var (_, cpMax) = GetAgeScaledRange(age, 10, 45, 100, 15, 1.0f, 30);
        string cpKey = "colorPerception";
        bool hasCpPrevious = previousRanges.TryGetValue(cpKey, out var prevCpRange);

        int colorPerception = stats.senses.sight.color_perception;
        if (hasCpPrevious && prevCpRange.max > prevCpRange.min)
        {
            float relPos = GetRelativePosition(colorPerception, prevCpRange.min, prevCpRange.max);
            colorPerception = Mathf.RoundToInt(ApplyRelativePosition(relPos, 0, cpMax));
        }
        stats.senses.sight.color_perception = Mathf.Clamp(colorPerception, 0, cpMax);
        previousRanges[cpKey] = (0, cpMax);

        // — hearing —
        // Applied the same logic for auditory acuity to allow for deafness.
        var (_, aaMax) = GetAgeScaledRange(age, 10, 30, 100, 20, 1.0f, 40);
        string aaKey = "auditoryAcuity";
        bool hasAaPrevious = previousRanges.TryGetValue(aaKey, out var prevAaRange);

        int auditoryAcuity = stats.senses.hearing.auditory_acuity;
        if (hasAaPrevious && prevAaRange.max > prevAaRange.min)
        {
            float relPos = GetRelativePosition(auditoryAcuity, prevAaRange.min, prevAaRange.max);
            auditoryAcuity = Mathf.RoundToInt(ApplyRelativePosition(relPos, 0, aaMax));
        }
        stats.senses.hearing.auditory_acuity = Mathf.Clamp(auditoryAcuity, 0, aaMax);
        previousRanges[aaKey] = (0, aaMax);

        var (fdMin, fdMax) = GetAgeScaledRange(age, 20, 40, 90, 10, 0.8f, 30);
        stats.senses.taste.flavor_discrimination = Mathf.Clamp(
            ClampRelative("flavorDiscrimination", stats.senses.taste.flavor_discrimination, fdMin, fdMax), fdMin, fdMax);
        
        var (_, lsMax) = GetAgeScaledRange(age, 18, 40, 90, 10, 0.9f, 30);
        stats.senses.sight.light_sensitivity = Mathf.Clamp(stats.senses.sight.light_sensitivity, 0, lsMax);

        var (_, nsMax) = GetAgeScaledRange(age, 5, 30, 90, 30, 1.0f, 25);
        stats.senses.hearing.noise_sensitivity = Mathf.Clamp(stats.senses.hearing.noise_sensitivity, 0, nsMax);

        var (hsMin, hsMax) = GetAgeScaledRange(age, 10, 40, 85, 60, 0.8f, 30);
        stats.senses.touch.heat_sensitivity = Mathf.Clamp(
            ClampRelative("touchHeat", stats.senses.touch.heat_sensitivity, hsMin, hsMax), hsMin, hsMax);
        stats.senses.touch.cold_sensitivity = Mathf.Clamp(
            ClampRelative("touchCold", stats.senses.touch.cold_sensitivity, hsMin, hsMax), hsMin, hsMax);

        var (ptMin, ptMax) = GetAgeScaledRange(age, 25, 60, 75, 5, 0.6f, 30); // Lowered base pain tolerance
        stats.senses.touch.pain_tolerance = Mathf.Clamp(
            ClampRelative("painTolerance", stats.senses.touch.pain_tolerance, ptMin, ptMax), ptMin, ptMax);

        // — infant override (unchanged as requested) —
        if (age < 2)
        {
            stats.senses.sight.visual_acuity = Mathf.Clamp(stats.senses.sight.visual_acuity, 0, 50);
            stats.senses.sight.color_perception = Mathf.Clamp(stats.senses.sight.color_perception, 0, 60);
        }
    }

    public static void ClampOrganHealth(characterStats stats)
    {
        if (stats == null) return;

        int years = stats.profile != null ? stats.profile.age.years : 30;

        // Ensure containers exist so we never NRE
        stats.internalOrgans ??= new characterStats.InternalOrgansStats();
        var io = stats.internalOrgans;
        io.heart        ??= new characterStats.InternalOrgansStats.HeartStats();
        io.heart.connections ??= new characterStats.InternalOrgansStats.HeartConnections();
        io.stomach      ??= new characterStats.InternalOrgansStats.StomachStats();

        // --- Coronary plaque ---
        var plaqueRange = GetAgeScaledRange(years, 85, 65, 80, 0, -0.5f, 30);
        const string plaqueKey = "coronaryPlaque";
        bool hasPrevPlaque = previousRanges.TryGetValue(plaqueKey, out var prevPlaque);
        int plaque = io.heart.coronary_arteries_plaque;
        if (hasPrevPlaque && prevPlaque.max > prevPlaque.min)
        {
            float rel = GetRelativePosition(plaque, prevPlaque.min, prevPlaque.max);
            plaque = Mathf.RoundToInt(ApplyRelativePosition(rel, plaqueRange.min, plaqueRange.max));
        }
        io.heart.coronary_arteries_plaque = Mathf.Clamp(plaque, plaqueRange.min, plaqueRange.max);
        previousRanges[plaqueKey] = plaqueRange;

        // --- Heartbeat regulation ---
        var regRange = GetAgeScaledRange(years, 30, 65, 95, 35, -0.5f, 30);
        const string regKey = "heartbeatRegulation";
        bool hasPrevReg = previousRanges.TryGetValue(regKey, out var prevReg);
        int regulation = io.heart.heartbeat_regulation;
        if (hasPrevReg && prevReg.max > prevReg.min)
        {
            float rel = GetRelativePosition(regulation, prevReg.min, prevReg.max);
            regulation = Mathf.RoundToInt(ApplyRelativePosition(rel, regRange.min, regRange.max));
        }
        io.heart.heartbeat_regulation = Mathf.Clamp(regulation, regRange.min, regRange.max);
        previousRanges[regKey] = regRange;

        // --- Heart strength ---
        var hsRange = GetAgeScaledRange(years, 30, 55, 90, 45, 1.0f, 25);
        const string hsKey = "heartStrength";
        bool hasPrevHs = previousRanges.TryGetValue(hsKey, out var prevHs);
        int heartStrength = io.heart.strength;
        if (hasPrevHs && prevHs.max > prevHs.min)
        {
            float rel = GetRelativePosition(heartStrength, prevHs.min, prevHs.max);
            heartStrength = Mathf.RoundToInt(ApplyRelativePosition(rel, hsRange.min, hsRange.max));
        }
        io.heart.strength = Mathf.Clamp(heartStrength, hsRange.min, hsRange.max);
        previousRanges[hsKey] = hsRange;

        // --- Metabolism (stomach) ---
        var metRange = GetAgeScaledRange(years, 20, 40, 95, 55, 1.0f, 25);
        const string metKey = "metabolism";
        bool hasPrevMet = previousRanges.TryGetValue(metKey, out var prevMet);
        int metabolism = io.stomach.metabolism;
        if (hasPrevMet && prevMet.max > prevMet.min)
        {
            float rel = GetRelativePosition(metabolism, prevMet.min, prevMet.max);
            metabolism = Mathf.RoundToInt(ApplyRelativePosition(rel, metRange.min, metRange.max));
        }
        io.stomach.metabolism = Mathf.Clamp(metabolism, metRange.min, metRange.max);
        previousRanges[metKey] = metRange;
    }


    /* ────────── GROUP: Qualities, Appearance & Reproduction Clamping ────────── */

    static int DynamicWidth(int age, int wMin, int wMax, int lerpStartAge, int lerpEndAge)
    {
        float t = Mathf.InverseLerp(lerpStartAge, lerpEndAge, age);
        return Mathf.RoundToInt(Mathf.Lerp(wMin, wMax, Mathf.Clamp01(t)));
    }

    static void ClampCognitive(characterStats stats)
    {
        // Bail if the container isn’t there (no generator dependency)
        if (stats?.profile == null || stats.qualities?.cognitive_abilities == null) return;

        int age = stats.profile.age.years;
        var cog = stats.qualities.cognitive_abilities;

        int wLogic = DynamicWidth(age, 25, 50, 5, 20);
        int wFocus = DynamicWidth(age, 25, 45, 5, 20);
        int wCreat = DynamicWidth(age, 30, 60, 4, 18);

        var (_, lgMax) = GetAgeScaledRange(age, 28f, 65f, 100f, 2f, 0.5f, wLogic);
        var (_, iqMax) = GetAgeScaledRange(age, 28f, 65f, 100f, 2f, 0.5f, wLogic);
        var (_, fcMax) = GetAgeScaledRange(age, 30f, 60f, 100f, 2f, 0.6f, wFocus);
        var (_, crMax) = GetAgeScaledRange(age, 25f, 65f, 100f, 5f, 0.4f, wCreat);

        cog.logic        = Mathf.Clamp(ClampRelative("logic",        cog.logic,        0, lgMax), 0, lgMax);
        cog.intelligence = Mathf.Clamp(ClampRelative("intelligence", cog.intelligence, 0, iqMax), 0, iqMax);
        cog.focus        = Mathf.Clamp(ClampRelative("focus",        cog.focus,        0, fcMax), 0, fcMax);
        cog.creativity   = Mathf.Clamp(ClampRelative("creativity",   cog.creativity,   0, crMax), 0, crMax);
    }

    public static void ClampQualities(characterStats stats)
    {
        if (stats?.profile == null || stats.qualities == null) return;

        // Cognitive first
        ClampCognitive(stats);

        int age = stats.profile.age.years;

        // Identity (null-safe)
        var id = stats.qualities.identity;
        if (id != null)
        {
            var (_, saMax) = GetAgeScaledRange(age, 45, 80, 100, 2, 0.2f, 40);
            id.self_awareness = Mathf.Clamp(ClampRelative("selfAwareness", id.self_awareness, 0, saMax), 0, saMax);

            var (_, seMax) = GetAgeScaledRange(age, 45, 75, 100, 2, 0.5f, 50);
            id.self_esteem = Mathf.Clamp(ClampRelative("selfEsteem", id.self_esteem, 0, seMax), 0, seMax);
        }

        // Social skills (null-safe)
        var soc = stats.qualities.social_skills;
        if (soc != null)
        {
            var (_, soMax) = GetAgeScaledRange(age, 25, 65, 100, 5, 0.4f, 50);
            soc.sociality = Mathf.Clamp(ClampRelative("sociality", soc.sociality, 0, soMax), 0, soMax);
        }
    }
    
    public static void ClampAppearance(characterStats stats)
    {
        if (stats?.profile == null) return;

        const int YOUTH_AGE_CUTOFF_YEARS = 18;
        const int YOUTH_STRETCHMARK_MAX  = 20;

        int years = stats.profile.age.years;
        if (years >= YOUTH_AGE_CUTOFF_YEARS) return;

        static void Cap(ref int v, int max) { if (v > max) v = max; }

        if (stats.head?.skin != null)  Cap(ref stats.head.skin.stretch_marks,  YOUTH_STRETCHMARK_MAX);
        if (stats.neck?.skin != null)  Cap(ref stats.neck.skin.stretch_marks,  YOUTH_STRETCHMARK_MAX);
        if (stats.torso?.skin != null) Cap(ref stats.torso.skin.stretch_marks, YOUTH_STRETCHMARK_MAX);

        if (stats.arms?.left?.skin  != null) Cap(ref stats.arms.left.skin.stretch_marks,  YOUTH_STRETCHMARK_MAX);
        if (stats.arms?.right?.skin != null) Cap(ref stats.arms.right.skin.stretch_marks, YOUTH_STRETCHMARK_MAX);

        if (stats.legs?.left?.skin  != null) Cap(ref stats.legs.left.skin.stretch_marks,  YOUTH_STRETCHMARK_MAX);
        if (stats.legs?.right?.skin != null) Cap(ref stats.legs.right.skin.stretch_marks, YOUTH_STRETCHMARK_MAX);
    }

    static int JitterDeterministic(int baseVal, int spread, int index, int salt)
    {
        // Stable “random” per index + baseVal using Perlin
        float n = Mathf.PerlinNoise((baseVal + salt) * 0.031f, index * 0.173f);
        int offset = Mathf.RoundToInt((n - 0.5f) * 2f * spread);
        return Mathf.Clamp(baseVal + offset, 0, 100);
    }

    public static void ApplyTeethFromGlobals(characterStats stats)
    {
        var t = stats?.head?.teeth;
        if (t == null) return;

        int targetW = Mathf.Clamp(t.overall_whiteness, 0, 100);
        int targetA = Mathf.Clamp(t.overall_alignment, 0, 100);

        int spreadW = (targetW == 0 || targetW == 100) ? 0 : 4;
        int spreadA = (targetA == 0 || targetA == 100) ? 0 : 4;

        int idx = 0;
        int Jitter(int baseVal, int spread, int salt)
        {
            float n = Mathf.PerlinNoise((baseVal + salt) * 0.031f, idx * 0.173f);
            int off = Mathf.RoundToInt((n - 0.5f) * 2f * spread);
            idx++; return Mathf.Clamp(baseVal + off, 0, 100);
        }

        void ApplyToTooth(characterStats.HeadStats.Tooth tooth)
        {
            if (tooth == null) return;
            tooth.whiteness = (spreadW == 0) ? targetW : Jitter(targetW, spreadW, 137);
            tooth.alignment = (spreadA == 0) ? targetA : Jitter(targetA, spreadA, 911);
        }

        var quads = new[]{
            t.upper_right, t.upper_left, t.lower_right, t.lower_left
        };
        foreach (var q in quads)
        {
            if (q == null || q.incisors == null || q.premolars == null || q.molars == null) continue;
            if (q.incisors.Length >= 2) { ApplyToTooth(q.incisors[0]); ApplyToTooth(q.incisors[1]); }
            ApplyToTooth(q.canine);
            if (q.premolars.Length >= 2) { ApplyToTooth(q.premolars[0]); ApplyToTooth(q.premolars[1]); }
            if (q.molars.Length    >= 2) { ApplyToTooth(q.molars[0]);    ApplyToTooth(q.molars[1]); }
        }
    }


    public static void ClampReproductive(characterStats stats)
    {
        // Guard refs first (profile & pelvis tree)
        if (stats == null || stats.profile == null) return;
        var pr = stats.pelvisAndRepro;
        if (pr == null || pr.genitals == null) return;

        int ageYears = stats.profile.age.years;

        if (pr.genitals is characterStats.PelvisReproductiveStats.FemaleGenitals female)
        {
            if (ageYears < 8)
            {
                female.uterus.cycles_active = false;
                female.uterus.cycle_day = 0;
                female.uterus.bleeding = false;
                female.uterus.fertility_factor = 0;
            }
            if (ageYears >= 55)
            {
                female.uterus.menopausal = true;
                female.uterus.cycles_active = false;
            }
            if (female.uterus.cycles_active)
            {
                int maxFertility = Mathf.Clamp(100 - Mathf.Max(0, ageYears - 18) * 2, 10, 100);
                female.uterus.fertility_factor = Mathf.Clamp(female.uterus.fertility_factor, 0, maxFertility);
            }
        }
    }


    public static int SampleHighBiased(int lo, int hi, float bias = 2.4f)
    {
        // bias > 1 skews toward the high end of [lo, hi]
        float t = Mathf.Pow(UnityEngine.Random.value, 1f / Mathf.Max(1.0001f, bias));
        return Mathf.RoundToInt(Mathf.Lerp(lo, hi, t));
    }

    public static void ClampVoice(characterStats stats)
    {
        if (stats?.profile?.voice == null) return;

        int age = stats.profile.age.years;
        var sex = stats.profile.sex;

        // ── PITCH (0 = lowest human ever, 100 = highest human ever; lower = deeper) ──
        int pMin, pMax;
        if (age <= 5)
        {
            pMin = 65; pMax = 100;                   // toddlers: very high only
        }
        else if (age <= 11)
        {
            pMin = 58; pMax = 100;                   // children: high
        }
        else if (age <= 14)
        {
            if (sex == characterStats.SexType.Male) { pMin = 50; pMax = 100; }   // early puberty (boys start dropping)
            else                                     { pMin = 55; pMax = 100; }  // girls stay high
        }
        else if (age <= 17)
        {
            if (sex == characterStats.SexType.Male) { pMin = 5;  pMax = 98;  }   // late puberty can be very deep
            else                                     { pMin = 35; pMax = 100; }  // teens (female) still not near absolute lows
        }
        else
        {
            // Adults: men can hit absolute low but not the absolute high; women vice-versa.
            if (sex == characterStats.SexType.Male)      { pMin = 0;  pMax = 88;  }
            else if (sex == characterStats.SexType.Female){ pMin = 44; pMax = 100; }
            else                                         { pMin = 20; pMax = 96;  } // intersex/other: broad but not both extremes
        }

        const string pKey = "voicePitch";
        int p = stats.profile.voice.pitch;
        if (previousRanges.TryGetValue(pKey, out var prevP) && prevP.max > prevP.min)
        {
            float rel = GetRelativePosition(p, prevP.min, prevP.max);
            p = Mathf.RoundToInt(ApplyRelativePosition(rel, pMin, pMax));
        }
        stats.profile.voice.pitch = Mathf.Clamp(p, pMin, pMax);
        previousRanges[pKey] = (pMin, pMax);

        // ── TONE & TIMBRE (0 = darkest, 100 = brightest) ──
        int tMin, tMax;
        if (age <= 5)      { tMin = 12; tMax = 100; }
        else if (age <= 11){ tMin = 10; tMax = 100; }
        else if (age <= 17){ tMin = 6;  tMax = 100; }
        else               { tMin = 0;  tMax = 100; } // adults can span darkest→brightest

        const string tKey = "voiceTone";
        const string bKey = "voiceTimbre";

        int tone = stats.profile.voice.tone;
        if (previousRanges.TryGetValue(tKey, out var prevT) && prevT.max > prevT.min)
        {
            float rel = GetRelativePosition(tone, prevT.min, prevT.max);
            tone = Mathf.RoundToInt(ApplyRelativePosition(rel, tMin, tMax));
        }
        stats.profile.voice.tone = Mathf.Clamp(tone, tMin, tMax);
        previousRanges[tKey] = (tMin, tMax);

        int timbre = stats.profile.voice.timbre;
        if (previousRanges.TryGetValue(bKey, out var prevB) && prevB.max > prevB.min)
        {
            float rel = GetRelativePosition(timbre, prevB.min, prevB.max);
            timbre = Mathf.RoundToInt(ApplyRelativePosition(rel, tMin, tMax));
        }
        stats.profile.voice.timbre = Mathf.Clamp(timbre, tMin, tMax);
        previousRanges[bKey] = (tMin, tMax);

    }



    public static void ClampBrain(characterStats stats)
    {
        // Guard refs
        if (stats == null || stats.profile == null) return;
        var brain = stats.head?.brain;
        if (brain == null) return;

        // No null-chaining on age
        int age = stats.profile.age.years;

        (int myMin, int myMax) = GetAgeScaledRange(age, 5, 30, 100, 55, -0.6f, 35);
        brain.myelin = Mathf.Clamp(ClampRelative("brainMyelin", brain.myelin, myMin, myMax), myMin, myMax);

        (int plMin, int plMax) = GetAgeScaledRange(age, 65, 85, 85, 0, -0.6f, 25);
        brain.plaque = Mathf.Clamp(ClampRelative("brainPlaque", brain.plaque, plMin, plMax), plMin, plMax);

        (int srMin, int srMax) = GetAgeScaledRange(age, 22, 55, 95, 65, 0.7f, 30);
        brain.shape_regulation = Mathf.Clamp(ClampRelative("brainShapeReg", brain.shape_regulation, srMin, srMax), srMin, srMax);

        (int inflMin, int inflMax) = GetAgeScaledRange(age, 75, 65, 65, 2, -0.5f, 30);
        brain.inflammation = Mathf.Clamp(ClampRelative("brainInflammation", brain.inflammation, inflMin, inflMax), inflMin, inflMax);

        (int linMin, int linMax) = GetAgeScaledRange(age, 22, 50, 95, 70, 0.9f, 30);
        brain.lining = Mathf.Clamp(ClampRelative("brainLining", brain.lining, linMin, linMax), linMin, linMax);

        (int bloodMin, int bloodMax) = GetAgeScaledRange(age, 22, 55, 95, 65, 0.8f, 30);
        brain.blood_amount = Mathf.Clamp(ClampRelative("brainBlood", brain.blood_amount, bloodMin, bloodMax), bloodMin, bloodMax);

        (int cavMin, int cavMax) = GetAgeScaledRange(age, 65, 55, 55, 8, -0.4f, 30);
        brain.cavity_pressure = Mathf.Clamp(ClampRelative("brainCavityPressure", brain.cavity_pressure, cavMin, cavMax), cavMin, cavMax);

        (int leakMin, int leakMax) = GetAgeScaledRange(age, 85, 75, 35, 0, -0.2f, 35);
        brain.spinal_fluid_leak = Mathf.Clamp(ClampRelative("brainLeak", brain.spinal_fluid_leak, leakMin, leakMax), leakMin, leakMax);

        brain.bruised = Mathf.Clamp(brain.bruised, 0, 100);
    }


    // small helper to keep relative positions for ints
    static int ClampRelative(string key, int current, int min, int max)
    {
        if (previousRanges.TryGetValue(key, out var prev) && prev.max > prev.min)
        {
            float rel = GetRelativePosition(current, prev.min, prev.max);
            current = Mathf.RoundToInt(ApplyRelativePosition(rel, min, max));
        }
        previousRanges[key] = (min, max);
        return current;
    }

    public static void ClampSkills(characterStats stats)
    {
        if (stats?.skills?.list == null || stats.profile == null) return;

        int ageY = stats.profile.age.years;
        float ageF = ageY + (stats.profile.age.months / 12f);

        foreach (var e in stats.skills.list)
        {
            if (e == null) continue;

            if (e.mode == characterStats.SkillMode.LearnOnce)
            {
                var (start, full) = LearnOnceWindowForLimits(e.id);
                float allowed = Mathf.Clamp01(Mathf.InverseLerp(start, full, ageF));
                int maxProgress = Mathf.RoundToInt(allowed * 100f);
                if (!e.learned)
                {
                    e.progress = Mathf.Clamp(e.progress, 0, maxProgress);
                    e.learned = (e.progress >= 100);
                }
                else e.progress = 100;
            }
            else
            {
                int awarenessCap = (ageY < 10) ? 0 : (ageY < 18 ? 1 : 2);
                e.awareness = Mathf.Clamp(e.awareness, 0, awarenessCap);

                string id = (e.id ?? "").ToLowerInvariant();
                (int min, int max) perfCap;

                if (id.Contains("sprint") || id.Contains("push") || id.Contains("lift") ||
                    id.Contains("climb") || id.Contains("jump") || id.Contains("bench") ||
                    id.Contains("pullups") || id.Contains("situp"))
                    perfCap = GetAgeScaledRange(ageY, 25, 40, 98, 15, 0.9f, 35);
                else if (id.Contains("drawing") || id.Contains("painting") || id.Contains("meditation") ||
                        id.Contains("decision") || id.Contains("problem") || id.Contains("visualization"))
                    perfCap = GetAgeScaledRange(ageY, 30, 60, 95, 30, 0.8f, 35);
                else if (id.Contains("public speaking") || id.Contains("presentation") || id.Contains("debate") ||
                        id.Contains("rhetoric") || id.Contains("persuasion") || id.Contains("negotiation") ||
                        id.Contains("conflict") || id.Contains("feedback") || id.Contains("storytelling"))
                    perfCap = GetAgeScaledRange(ageY, 32, 65, 92, 18, 0.7f, 35);
                else if (id.Contains("driv") || id.Contains("navigation"))
                    perfCap = GetAgeScaledRange(ageY, 20, 65, 98, 0, 0.8f, 35);
                else
                    perfCap = GetAgeScaledRange(ageY, 25, 60, 92, 22, 0.8f, 35);

                int cap = perfCap.max;
                e.freshness  = Mathf.Clamp(e.freshness,  0, cap);
                e.confidence = Mathf.Clamp(e.confidence, 0, cap);
                e.efficiency = Mathf.Clamp(e.efficiency, 0, cap);
                e.precision  = Mathf.Clamp(e.precision,  0, cap);
            }
        }
    }

    // Copy of the generator’s windows for consistency (read-only for clamping)
    static (float start, float full) LearnOnceWindowForLimits(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        switch (k)
        {
            case "sitting":  return (0.25f, 0.75f);
            case "crawling": return (0.50f, 1.10f);
            case "walking":  return (0.75f, 1.40f);
            case "kneel":    return (0.80f, 2.00f);
            case "crouch":   return (0.80f, 2.00f);
            case "genuflect":return (2.50f, 6.00f);

            case "showering/bathing":
            case "showering":
            case "bathing":      return (3.00f, 7.00f);
            case "handwashing":
            case "wash hands":   return (2.00f, 5.00f);
            case "brush teeth":  return (2.50f, 7.00f);
            case "floss":        return (4.50f, 10.00f);
            case "wash face":    return (2.50f, 5.00f);
            case "rinse mouth":  return (2.50f, 4.50f);
            case "wash hair":    return (3.50f, 8.00f);
            case "nail care":    return (4.00f, 9.00f);

            case "open/close":
            case "open":
            case "close":        return (0.50f, 1.50f);
            case "place":        return (0.50f, 1.50f);
            case "pour":         return (2.00f, 4.00f);

            case "wave":         return (0.60f, 2.00f);
            case "nod":          return (0.80f, 2.00f);
            case "wink":         return (3.50f, 7.00f);

            case "toilet independence": return (2.00f, 3.50f);
            case "counting":            return (3.00f, 6.00f);
            case "reading":             return (4.00f, 8.00f);
            case "writing":             return (4.50f, 8.50f);
            case "ride bicycle":        return (3.00f, 8.00f);
            case "swim basics":         return (3.00f, 10.00f);
            case "drive":               return (16.00f, 19.00f);
            default: return (2.0f, 10.0f);
        }
    }






    /* ────────────── Master Recompute Function ────────────── */

    public static void RecomputeLimits(characterStats stats)
    {
        ClampHeight(stats);
        ClampWeight(stats);
        ClampFootSize(stats);
        ClampStrength(stats);
        ClampBodyComposition(stats);
        ClampFlexibilityAndFatigue(stats);
        ClampSenses(stats);                 // (extended below)
        ClampOrganHealth(stats);
        ClampBrain(stats);                  // ← NEW
        ClampVoice(stats);                  // ← NEW
        ClampQualities(stats);
        ClampAppearance(stats);
        ApplyTeethFromGlobals(stats);
        ClampReproductive(stats);
        ClampSkills(stats);                 // ← NEW (last is fine)
    }

    
    /// <summary>
    /// Reset all stored previous ranges. Call this when creating a new character
    /// or when you want the next clamp operation to not use relative positioning.
    /// </summary>
    public static void ResetPreviousRanges()
    {
        previousHeightRange = (-1, -1);
        previousWeightRange = (-1, -1);
        previousRanges.Clear();
    }
}