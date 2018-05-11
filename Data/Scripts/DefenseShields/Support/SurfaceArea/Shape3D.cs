namespace DefenseShields.Support
{
    public abstract class Shape3D
    {
        protected double a { get; set; }
        protected double b { get; set; }
        protected double c { get; set; }

        public abstract double Volume { get; }
        public abstract double Surface { get; }
    }
}
