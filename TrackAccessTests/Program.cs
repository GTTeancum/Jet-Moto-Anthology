using JetMoto;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    passed++;
}
// Original four difficulty rows, independently read from SCUS_943.09's
// 0x8018A9BC table. Default difficulty exposes slots 0,3,4 only.
byte[][] rows = [
    [1,0,0,1,1,0,0,0,0,0],
    [1,1,1,1,1,0,0,1,0,1],
    [1,1,1,1,1,0,1,1,1,1],
    [1,1,1,1,1,1,1,1,1,1]
];
foreach (bool enabled in new[] { false,true,false })
{
    TrackAccess.Configure(enabled);
    foreach(uint variant in new uint[] {0,1,2,3,4,uint.MaxValue})
        Check(TrackAccess.MenuArtworkVariant(variant)==(enabled && variant<4?3:variant),
              "Artwork override must be scoped to known menu variants");
    foreach (var row in rows)
        for(uint slot=0;slot<10;slot++)
            Check(TrackAccess.Availability(slot,row[slot])==(enabled?1:row[slot]),
                  $"enabled={enabled}, slot={slot}, original={row[slot]}");
    foreach(uint slot in new uint[] {10,11,uint.MaxValue})
        foreach(uint original in new uint[] {0,1,255})
            Check(TrackAccess.Availability(slot,original)==original,"Out-of-range slot must be untouched");
}
Console.WriteLine($"PASS: {passed} track access checks, including default behavior after disabling.");
