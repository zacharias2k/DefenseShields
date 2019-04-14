namespace DefenseSystems
{
    public partial class Controllers
    {
        internal bool PowerOnline()
        {
            if (!Bus.HasPower()) return false;

            SinkPower = Bus.PowerForUse;
            if (Bus.PowerUpdate) Sink.Update();


            return true;
        }
    }
}