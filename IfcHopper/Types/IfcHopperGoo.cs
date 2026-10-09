using Grasshopper.Kernel.Types;

namespace IfcHopper.Types
{
    /// <summary>
    /// Base Grasshopper wrapper for IfcHopper Core objects. Data is passed by reference:
    /// components must not modify incoming objects, they create new ones.
    /// </summary>
    public abstract class IfcHopperGoo<T> : GH_Goo<T> where T : class
    {
        protected IfcHopperGoo() { }

        protected IfcHopperGoo(T value) : base(value) { }

        public override bool IsValid => Value != null;

        public override bool CastFrom(object source)
        {
            if (source is T value)
            {
                Value = value;
                return true;
            }
            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (Value is Q value)
            {
                target = value;
                return true;
            }
            return false;
        }
    }
}
