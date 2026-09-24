using System.Buffers.Binary;

namespace JetMoto;

/// <summary>Distance selectors whose complete original subtree is verified water.</summary>
public static class WaterLodPolicy
{
    public static HashSet<int> FindSelectors(byte[] source, IReadOnlySet<int> waterMeshes,Action<string>? diagnostic=null)
    {
        HashSet<int> selectors=[];
        Dictionary<int,bool> visited=[];
        HashSet<int> visiting=[];
        void Range(int offset,int length)
        {
            if(offset<0||length<0||offset>source.Length-length)throw new InvalidDataException("Water LOD source bounds");
        }
        uint U(int offset){Range(offset,4);return BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset,4));}
        int Pointer(int offset)
        {
            long value=(long)U(offset)-U(12);
            if(value<0||value>=source.Length)throw new InvalidDataException("Water LOD source pointer");
            return (int)value;
        }
        bool Visit(int offset,int depth)
        {
            if(depth>64||!visiting.Add(offset))throw new InvalidDataException("Water LOD source cycle/depth");
            if(visited.TryGetValue(offset,out bool known)){visiting.Remove(offset);return known;}
            Range(offset,1);
            int type=source[offset],count=0,start=0;
            switch(type){
                case 0: visiting.Remove(offset);return visited[offset]=waterMeshes.Contains(offset);
                case 1: Range(offset,24);count=source[offset+20];start=24;break;
                case 2: Range(offset,20);if(U(offset+16)>256)throw new InvalidDataException("Water LOD child count");count=(int)U(offset+16);start=20;break;
                case 3: Range(offset,32);count=source[offset+11];start=32;break;
                case 4: Range(offset,20);count=source[offset+18];start=20;break;
                case 5: Range(offset,40);count=source[offset+38];start=40;break;
                case 9: Range(offset,16);count=source[offset+6];start=16;break;
                case 11: Range(offset,16);count=2;start=8;break;
                case 12: Range(offset,8);count=BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset+6,2));start=8;break;
                default: visiting.Remove(offset);return visited[offset]=false;
            }
            Range(offset+start,count*4);
            bool water=count>0;
            for(int i=0;i<count;i++){
                int child=Pointer(offset+start+i*4);
                if(type==2){
                    Range(child,12);
                    if(U(child)<=U(child+4))throw new InvalidDataException($"Water LOD interval at {offset:X}: {U(child+4)}..{U(child)}");
                    child=Pointer(child+8);
                }
                // Evaluate every child; a mixed parent may still contain pure water selectors.
                bool childWater=Visit(child,depth+1);water &= childWater;
            }
            if(type==2&&water)selectors.Add(offset);
            visiting.Remove(offset);return visited[offset]=water;
        }
        try{
            Range(0,28);
            uint roots=U(24);if(roots>4096)throw new InvalidDataException("Water LOD roots");
            Range(28,(int)roots*4);
            for(int i=0;i<roots;i++)Visit(Pointer(28+i*4),0);
        }catch(InvalidDataException e){diagnostic?.Invoke(e.Message);selectors.Clear();}
        return selectors;
    }

    // Native type-2 selection compares squared camera distance after model scaling.
    public static uint ExtendDistance(uint squaredDistance)=>squaredDistance/16;
}
