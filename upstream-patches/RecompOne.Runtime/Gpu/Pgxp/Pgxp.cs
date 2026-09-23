using RecompOne.Runtime.Config;

namespace RecompOne.Runtime.Pgxp;

public static class Pgxp
{
    public static bool Shown = true;

    // Game-selected policy, not a saved preference. Reloading/resetting UI settings
    // cannot silently bring affine mapping back. Other RecompOne games are unaffected.
    public static bool RequiredPerspective { get; private set; }
    public static void RequirePerspectiveCorrection()
    {
        RequiredPerspective = true;
        Shown = false;
        Load();
    }
    
    public const string KeyEnable = "pgxp.enable";
    public const string KeyCulling = "pgxp.culling";
    public const string KeyTextureCorrection = "pgxp.texture_correction";
    public const string KeyVertexCache = "pgxp.vertex_cache";
    public const string KeyCacheW = "pgxp.cache_w";
    public const string KeyCpu = "pgxp.cpu";
    public const string KeyMemory = "pgxp.memory";
    public const string KeyTolerance = "pgxp.tolerance";
    
    public const float DefaultTolerance = 2f;
    
    private static bool _loaded;
    
    public static bool Enabled { get; private set; }
    public static bool Culling { get; private set; }
    public static bool TextureCorrection { get; private set; }
    public static bool VertexCache { get; private set; }
    public static bool CacheW { get; private set; }
    public static bool CpuTracking { get; private set; }
    public static bool MemoryTracking { get; private set; }
    public static float Tolerance { get; private set; }
    
    public static bool CullingCorrection => Enabled && Culling;
    
    public static void Load()
    {
        var view = ConfigManager.View;

        Enabled = view.GetBool(KeyEnable);
        Culling = view.GetBool(KeyCulling, true);
        TextureCorrection = view.GetBool(KeyTextureCorrection, true);
        VertexCache = view.GetBool(KeyVertexCache, true);
        CacheW = VertexCache && view.GetBool(KeyCacheW, true);
        CpuTracking = Enabled && view.GetBool(KeyCpu, true);
        MemoryTracking = Enabled && view.GetBool(KeyMemory, true);
        Tolerance = view.GetFloat(KeyTolerance, DefaultTolerance);
        
        if (RequiredPerspective)
        {
            Enabled = TextureCorrection = CpuTracking = MemoryTracking = true;
            // Keep the game's original visibility/backface decisions, and never
            // borrow depth from unrelated vertices which happen to share screen XY.
            Culling = VertexCache = CacheW = false;
            Tolerance = DefaultTolerance;
            Shown = false;
        }
        _loaded = true;
    }
    
    public static void EnsureLoaded()
    {
        if (!_loaded) Load();
    }
}