using System;

namespace TickZoom.Api
{
    public class SerializeMemberAttribute : Attribute
    {
        private int id;
        public SerializeMemberAttribute(int i)
        {
            this.id = i;
        }

        public int Id
        {
            get { return id; }
        }
    }
}