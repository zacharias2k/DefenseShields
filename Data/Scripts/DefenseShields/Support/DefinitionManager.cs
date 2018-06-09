using System.Collections.Generic;

namespace DefenseShields.Support
{
    public static class DefinitionManager
    {
        private static readonly Dictionary<string, Definition> Def = new Dictionary<string, Definition>
        {
            ["DefenseShieldsLS"] = new Definition { Name = "DefenseShieldsLS", ParticleScale = 10f, ParticleDist = 1.5d, HelperDist = 5.0d, FieldDist = 4.5d },
            ["DefenseShieldsSS"] = new Definition { Name = "DefenseShieldsSS", ParticleScale = 2.5f, ParticleDist = 1.25d, HelperDist = 3d, FieldDist = 0.8d },
            ["DefenseShieldsST"] = new Definition { Name = "DefenseShieldsST", ParticleScale = 20f, ParticleDist = 3.5d, HelperDist = 7.5d, FieldDist = 8.0d },
        };


        public static Definition Get(string subtype)
        {
            return Def.GetValueOrDefault(subtype);
        }
    }

    public class Definition
    {
        public string Name;
        public float ParticleScale;
        public double ParticleDist;
        public double HelperDist;
        public double FieldDist;
    }
}
