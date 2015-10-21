// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace MonoGame.Utilities
{
    /// <summary>
    /// A 32.32 fixed point implementation used by Audio.Mixer.
    /// </summary>
    internal struct Fix64
    {
        Int64 _value;

        /// <summary>
        /// A constant with the value of 0.0.
        /// </summary>
        public static Fix64 Zero { get { return new Fix64(0, 0); } }

        /// <summary>
        /// A constant with the value of 1.0.
        /// </summary>
        public static Fix64 One { get { return new Fix64(1, 0); } }

        static double FractMax = (double)UInt32.MaxValue;

        /// <summary>
        /// Creates an instance of Fix64 with the value already encoded in 32.32 format.
        /// </summary>
        /// <param name="value">The value encoded in 32.32 format.</param>
        public Fix64(Int64 value)
        {
            _value = value;
        }

        /// <summary>
        /// Creates an instance of Fix64 with the given floating point value.
        /// </summary>
        /// <param name="value">The floating-point value.</param>
        public Fix64(double value)
        {
            Int64 a = (Int64)value << 32;
            UInt32 b = (UInt32)(((double)value - (int)value) * FractMax);
            _value = a | b;
        }

        /// <summary>
        /// Creates an instance of Fix64 with the given integral and fractional values.
        /// </summary>
        /// <param name="index">The integral portion of the value.</param>
        /// <param name="fract">The fractional portion of the value.</param>
        public Fix64(UInt32 index, UInt32 fract)
        {
            _value = ((Int64)index << 32) | fract;
        }

        /// <summary>
        /// Gets the integral portion of the value.
        /// </summary>
        public UInt32 Index
        {
            get
            {
                return (UInt32)(_value >> 32);
            }
        }

        /// <summary>
        /// Gets the fractional portion of the value.
        /// </summary>
        public UInt32 Fract
        {
            get
            {
                return (UInt32)_value;
            }
        }

        /// <summary>
        /// Gets the rounded integral value.
        /// </summary>
        public UInt32 RoundedIndex
        {
            get
            {
                return (UInt32)((_value + 0x80000000) >> 32);
            }
        }

        /// <summary>
        /// Gets the value as a double.
        /// </summary>
        /// <returns>The value as a double.</returns>
        public double ToDouble()
        {
            return (double)Index + (double)Fract / FractMax;
        }

        /// <summary>
        /// Adds two Fix64 instances.
        /// </summary>
        /// <param name="f1">The first operand.</param>
        /// <param name="f2">The second operand.</param>
        /// <returns>The sum of the two operands.</returns>
        public static Fix64 operator +(Fix64 f1, Fix64 f2)
        {
            return new Fix64(f1._value + f2._value);
        }

        /// <summary>
        /// Subtracts two Fix64 instances.
        /// </summary>
        /// <param name="f1">The first operand.</param>
        /// <param name="f2">The second operand.</param>
        /// <returns>The difference between the two operands.</returns>
        public static Fix64 operator -(Fix64 f1, Fix64 f2)
        {
            return new Fix64(f1._value - f2._value);
        }

        /// <summary>
        /// Adds one to the value.
        /// </summary>
        /// <param name="f1">The object to increment.</param>
        /// <returns>The supplied object plus one.</returns>
        public static Fix64 operator ++(Fix64 f1)
        {
            return new Fix64(f1._value + (Int64)0x100000000);
        }

        /// <summary>
        /// Compares two Fix64 instances for equality.
        /// </summary>
        /// <param name="f1">The first operand.</param>
        /// <param name="f2">The second operand.</param>
        /// <returns>True if the two operands are equal.</returns>
        public static bool operator ==(Fix64 f1, Fix64 f2)
        {
            return f1._value == f2._value;
        }

        /// <summary>
        /// Compares two Fix64 instances for inequality.
        /// </summary>
        /// <param name="f1">The first operand.</param>
        /// <param name="f2">The second operand.</param>
        /// <returns>True if the two operands are not equal.</returns>
        public static bool operator !=(Fix64 f1, Fix64 f2)
        {
            return f1._value != f2._value;
        }

        /// <summary>
        /// Gets the hash code of the instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        /// <summary>
        /// Compares the given object with this instance.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object and this instance are equal.</returns>
        public override bool Equals(object obj)
        {
            return (obj is Fix64) && (_value == ((Fix64)obj)._value);
        }
    }
}
