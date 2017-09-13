/*
 * This source file is part of Spire (Synthesis of ProbabIlistic pRivacy Enforcements).
 * For more information, see the Spire project website at:
 *     http://www.srl.inf.ethz.ch/probabilistic-security
 * Copyright 2017 Software Reliability Lab, ETH Zurich
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Numerics;

namespace Spire
{
    public struct Rational
    {
        public BigInteger Numerator { get; }

        public BigInteger Denominator { get; }
        
        public Rational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero && numerator.IsZero)
            {
                denominator = BigInteger.One;
            }

            if (denominator.IsZero)
            {
                throw new ArgumentException("Denominator of a fraction can not be zero.", nameof(denominator));
            }
            
            BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        public static Rational operator +(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Denominator + right.Numerator * left.Denominator, left.Denominator * right.Denominator);
        }

        public static Rational operator -(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Denominator - right.Numerator * left.Denominator, left.Denominator * right.Denominator);
        }

        public static Rational operator *(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Numerator, left.Denominator * right.Denominator);
        }

        public static Rational operator /(Rational left, Rational right)
        {
            return new Rational(left.Numerator * right.Denominator, left.Denominator * right.Numerator);
        }

        public override string ToString()
        {
            return Numerator.ToString() + "/" + Denominator.ToString();// + "(" + ToDouble() + ")";
        }

        public double ToDouble()
        {
            return (double)Numerator / (double)Denominator;
        }
    }
}
