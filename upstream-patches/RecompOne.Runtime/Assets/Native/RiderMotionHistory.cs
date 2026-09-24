using System.Numerics;

namespace RecompOne.Runtime.Assets.Native;

public readonly record struct RiderPathSegment(int RiderId, Vector3 A, Vector3 B,
    float BornA, float BornB, float DistanceA, float DistanceB);

/// <summary>World-space path observations, independent of native effect emission.
/// Contact and material classification must still precede rendering an effect.</summary>
public sealed class RiderMotionHistory
{
    public const float Lifetime = 1.5f;
    private const float Spacing = 1.5f;
    private const int MaxPoints = 320;
    private readonly record struct Point(Vector3 Position, float Time, float Distance);
    private sealed class Track(Point first)
    {
        public Point Last = first;
        public Point Tip = first;
        public readonly List<Point> Points = [first];
    }
    private readonly Dictionary<int, Track> _tracks = [];
    private WorldScene? _scene;
    private WorldCamera? _lastCamera;
    private float _time = float.NegativeInfinity;
    private int _viewSlot;
    private RiderPathSegment[] _segments = [];

    public IReadOnlyList<RiderPathSegment> Observe(WorldCamera camera)
    {
        if (ReferenceEquals(camera, _lastCamera)) return _segments;
        _lastCamera = camera;
        float now = camera.Time;
        if (!float.IsFinite(now)) return _segments = [];
        if (!ReferenceEquals(_scene, camera.Scene) || _viewSlot != camera.ViewSlot || now < _time)
            _tracks.Clear();
        _scene = camera.Scene; _viewSlot = camera.ViewSlot; _time = now;

        foreach (int id in _tracks.Keys.ToArray())
            if (now - _tracks[id].Last.Time > Lifetime) _tracks.Remove(id);
        foreach (var (id, bounds) in camera.Riders)
        {
            if (id is < 200 or >= 220 || bounds.Samples == 0) continue;
            Vector3 position = (bounds.Min + bounds.Max) * .5f;
            if (!float.IsFinite(position.X + position.Y + position.Z)) continue;
            var first = new Point(position, now, 0);
            if (!_tracks.TryGetValue(id, out var track))
            {
                _tracks[id] = new Track(first);
                continue;
            }
            float dt = now - track.Last.Time;
            if (dt <= 0) continue;
            float travel = Vector3.Distance(position, track.Last.Position);
            // A reacquired actor or respawn must never draw a line across the course.
            if (dt > .25f || travel > 1000f * dt + 2f)
            {
                _tracks[id] = new Track(first);
                continue;
            }
            float distance = track.Last.Distance + travel;
            float nextDistance = track.Points[^1].Distance + Spacing;
            while (travel > .00001f && nextDistance <= distance)
            {
                float fraction = (nextDistance - track.Last.Distance) / travel;
                track.Points.Add(new Point(Vector3.Lerp(track.Last.Position, position, fraction),
                    track.Last.Time + dt * fraction, nextDistance));
                nextDistance += Spacing;
            }
            track.Last = new Point(position, now, distance);
            if (travel > .01f) track.Tip = track.Last;
            // Keep one predecessor for clipping the trail's fading end.
            while (track.Points.Count > 1 && track.Points[1].Time < now - Lifetime)
                track.Points.RemoveAt(0);
            if (track.Points.Count > MaxPoints)
                track.Points.RemoveRange(0, track.Points.Count - MaxPoints);
        }
        var segments = new List<RiderPathSegment>();
        foreach (var (id, track) in _tracks.OrderBy(p => p.Key))
        {
            for (int i = 1; i < track.Points.Count; i++) Add(track.Points[i - 1], track.Points[i]);
            Add(track.Points[^1], track.Tip);
            void Add(Point a, Point b)
            {
                if (b.Time < now - Lifetime || Vector3.DistanceSquared(a.Position, b.Position) < .0625f) return;
                segments.Add(new(id, a.Position, b.Position, a.Time, b.Time, a.Distance, b.Distance));
            }
        }
        return _segments = segments.ToArray();
    }
}
