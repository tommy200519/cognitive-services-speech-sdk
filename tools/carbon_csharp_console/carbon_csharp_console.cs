//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//
// carbon_csharp_console.cs: A console application for testing Carbon C# client library.
//

using System;
using System.Threading.Tasks;
using Carbon;
using Carbon.Recognition;
using Carbon.Recognition.Speech;

namespace CarbonSamples
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 0)
            {
                Console.WriteLine("Usage: carbon_csharp_console wavfile");
                Environment.Exit(1);
            }

            SpeechRecognitionSamples.SpeechRecognitionAsync(args[0]).Wait();

            IntentRecognitionSamples.IntentRecognitionAsync(args[0]).Wait();
        }
    }

}
