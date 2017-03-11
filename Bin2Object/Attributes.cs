using System;
using System.Collections.Generic;
using System.Text;

namespace NoisyCowStudios.Bin2Object
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ArrayLengthAttribute : Attribute
    {
        public string FieldName { get; set; }

        public ArrayLengthAttribute() { }

    }
}
