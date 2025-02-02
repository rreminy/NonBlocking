﻿// Copyright (c) Vladimir Sadov. All rights reserved.
//
// This file is distributed under the MIT License. See LICENSE.md for details.

#nullable disable

using System;
using System.Runtime.CompilerServices;

namespace NonBlocking
{
    internal abstract class DictionaryImpl
    {
        internal DictionaryImpl() { }

        internal enum ValueMatch
        {
            Any,            // sets new value unconditionally, used by index set and TryRemove(key)
            NullOrDead,     // set value if original value is null or dead, used by Add/TryAdd
            NotNullOrDead,  // set value if original value is alive, used by Remove
            OldValue,       // sets new value if old value matches
        }

        internal sealed class Prime
        {
            [ThreadStatic]
            private static Prime s_cachedPrime;
            public static Prime GetOrCreate(object originalValue)
            {
                Prime prime;
                if ((prime = s_cachedPrime) is not null)
                {
                    //Console.Write("C");
                    s_cachedPrime = null;
                }
                else
                {
                    //Console.Write("+");
                    prime = new Prime();
                }
                prime.originalValue = originalValue;
                return prime;
            }

            public static void Return(Prime prime)
            {
                //Console.Write("-");
                if (s_cachedPrime is not null) return;
                prime.originalValue = null;
                s_cachedPrime = prime;
            }
            
            internal object originalValue;

            private Prime() { /* Empty */ }

            private Prime(object originalValue)
            {
                this.originalValue = originalValue;
            }
        }

        internal static readonly object TOMBSTONE = new object();
        internal static readonly Prime TOMBPRIME = Prime.GetOrCreate(TOMBSTONE);
        internal static readonly object NULLVALUE = new object();

        // represents a trivially copied empty entry
        // we insert it in the old table during rehashing
        // to reduce chances that more entries are added
        protected const int TOMBPRIMEHASH = 1 << 31;

        // we cannot distigush zero keys from uninitialized state
        // so we force them to have this special hash instead
        protected const int ZEROHASH = 1 << 30;

        // all regular hashes have both these bits set
        // to be different from either 0, TOMBPRIMEHASH or ZEROHASH
        // having only these bits set in a case of Ref key means that the slot is permanently deleted.
        protected const int SPECIAL_HASH_BITS = TOMBPRIMEHASH | ZEROHASH;

        // Heuristic to decide if we have reprobed toooo many times.  Running over
        // the reprobe limit on a 'get' call acts as a 'miss'; on a 'put' call it
        // can trigger a table resize.  Several places must have exact agreement on
        // what the reprobe_limit is, so we share it here.
        protected const int REPROBE_LIMIT = 4;
        protected const int REPROBE_LIMIT_SHIFT = 8;

        protected static int ReprobeLimit(int lenMask)
        {
            // 1/2 of table with some extra
            return REPROBE_LIMIT + (lenMask >> REPROBE_LIMIT_SHIFT);
        }

        protected static bool EntryValueNullOrDead(object entryValue)
        {
            return entryValue == null || entryValue == TOMBSTONE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static int ReduceHashToIndex(int fullHash, int lenMask)
        {
            return fullHash & lenMask;
            /*
            var h = (uint)fullHash;

            // xor-shift some upper bits down, in case if variations are mostly in high bits
            // and scatter the bits a little to break up clusters if hashes are periodic (like 42, 43, 44, ...)
            // long clusters can cause long reprobes. small clusters are ok though.
            h ^= h >> 15;
            h ^= h >> 8;
            h += (h >> 3) * 2654435769u;

            return (int)h & lenMask;
            */
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static object ToObjectValue<TValue>(TValue value)
        {
            if (default(TValue) != null)
            {
                return new Boxed<TValue>(value);
            }

            return (object)value ?? NULLVALUE;
        }
    }
}
