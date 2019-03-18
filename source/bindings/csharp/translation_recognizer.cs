//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech.Internal;
using static Microsoft.CognitiveServices.Speech.Internal.SpxExceptionThrower;

namespace Microsoft.CognitiveServices.Speech.Translation
{
    /// <summary>
    /// Performs translation on the speech input.
    /// </summary>
    /// <example>
    /// An example to use the translation recognizer from microphone and listen to events generated by the recognizer.
    /// <code>
    /// public async Task TranslationContinuousRecognitionAsync()
    /// {
    ///     // Creates an instance of a speech translation config with specified subscription key and service region. 
    ///     // Replace with your own subscription key and service region (e.g., "westus").
    ///     var config = SpeechTranslationConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
    ///
    ///     // Sets source and target languages.
    ///     string fromLanguage = "en-US";
    ///     config.SpeechRecognitionLanguage = fromLanguage;
    ///     config.AddTargetLanguage("de");
    ///
    ///     // Sets voice name of synthesis output.
    ///     const string GermanVoice = "Microsoft Server Speech Text to Speech Voice (de-DE, Hedda)";
    ///     config.VoiceName = GermanVoice;
    ///     // Creates a translation recognizer using microphone as audio input.
    ///     using (var recognizer = new TranslationRecognizer(config))
    ///     {
    ///         // Subscribes to events.
    ///         recognizer.Recognizing += (s, e) =>
    ///         {
    ///             Console.WriteLine($"RECOGNIZING in '{fromLanguage}': Text={e.Result.Text}");
    ///             foreach (var element in e.Result.Translations)
    ///             {
    ///                 Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
    ///             }
    ///         };
    ///
    ///         recognizer.Recognized += (s, e) =>
    ///         {
    ///             if (e.Result.Reason == ResultReason.TranslatedSpeech)
    ///             {
    ///                 Console.WriteLine($"\nFinal result: Reason: {e.Result.Reason.ToString()}, recognized text in {fromLanguage}: {e.Result.Text}.");
    ///                 foreach (var element in e.Result.Translations)
    ///                 {
    ///                     Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
    ///                 }
    ///             }
    ///         };
    ///
    ///         recognizer.Synthesizing += (s, e) =>
    ///         {
    ///             var audio = e.Result.GetAudio();
    ///             Console.WriteLine(audio.Length != 0
    ///                 ? $"AudioSize: {audio.Length}"
    ///                 : $"AudioSize: {audio.Length} (end of synthesis data)");
    ///         };
    ///
    ///         recognizer.Canceled += (s, e) =>
    ///         {
    ///             Console.WriteLine($"\nRecognition canceled. Reason: {e.Reason}; ErrorDetails: {e.ErrorDetails}");
    ///         };
    ///
    ///         recognizer.SessionStarted += (s, e) =>
    ///         {
    ///             Console.WriteLine("\nSession started event.");
    ///         };
    ///
    ///         recognizer.SessionStopped += (s, e) =>
    ///         {
    ///             Console.WriteLine("\nSession stopped event.");
    ///         };
    ///
    ///         // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
    ///         Console.WriteLine("Say something...");
    ///         await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
    ///
    ///         do
    ///         {
    ///             Console.WriteLine("Press Enter to stop");
    ///         } while (Console.ReadKey().Key != ConsoleKey.Enter);
    ///
    ///         // Stops continuous recognition.
    ///         await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class TranslationRecognizer : Recognizer
    {
        /// <summary>
        /// The event <see cref="Recognizing"/> signals that an intermediate recognition result is received.
        /// </summary>
        public event EventHandler<TranslationRecognitionEventArgs> Recognizing;

        /// <summary>
        /// The event <see cref="Recognized"/> signals that a final recognition result is received.
        /// </summary>
        public event EventHandler<TranslationRecognitionEventArgs> Recognized;

        /// <summary>
        /// The event <see cref="Canceled"/> signals that the speech to text/synthesis translation was canceled.
        /// </summary>
        public event EventHandler<TranslationRecognitionCanceledEventArgs> Canceled;

        /// <summary>
        /// The event <see cref="Synthesizing"/> signals that a translation synthesis result is received.
        /// </summary>
        public event EventHandler<TranslationSynthesisEventArgs> Synthesizing;

        /// <summary>
        /// Creates a translation recognizer using the default microphone input for a specified translation configuration.
        /// </summary>
        /// <param name="config">Translation config.</param>
        /// <returns>A translation recognizer instance.</returns>
        public TranslationRecognizer(SpeechTranslationConfig config)
            : this(FromConfig(SpxFactory.recognizer_create_translation_recognizer_from_config, config))
        {
        }

        /// <summary>
        /// Creates a translation recognizer using the specified speech translator and audio configuration.
        /// </summary>
        /// <param name="config">Translation config.</param>
        /// <param name="audioConfig">Audio config.</param>
        /// <returns>A translation recognizer instance.</returns>
        public TranslationRecognizer(SpeechTranslationConfig config, Audio.AudioConfig audioConfig)
            : this(FromConfig(SpxFactory.recognizer_create_translation_recognizer_from_config, config, audioConfig))
        {
            this.audioConfig = audioConfig;
        }

        internal TranslationRecognizer(InteropSafeHandle recoHandle) : base(recoHandle)

        {
            recognizingCallbackDelegate = FireEvent_Recognizing;
            recognizedCallbackDelegate = FireEvent_Recognized;
            canceledCallbackDelegate = FireEvent_Canceled;
            translationSynthesisCallbackDelegate = FireEvent_SynthesisResult;

            ThrowIfNull(recoHandle, "Invalid recognizer handle");
            ThrowIfFail(Internal.Recognizer.recognizer_recognizing_set_callback(recoHandle, recognizingCallbackDelegate, GCHandle.ToIntPtr(gch)));
            ThrowIfFail(Internal.Recognizer.recognizer_recognized_set_callback(recoHandle, recognizedCallbackDelegate, GCHandle.ToIntPtr(gch)));
            ThrowIfFail(Internal.Recognizer.recognizer_canceled_set_callback(recoHandle, canceledCallbackDelegate, GCHandle.ToIntPtr(gch)));
            ThrowIfFail(Internal.Recognizer.translator_synthesizing_audio_set_callback(recoHandle, translationSynthesisCallbackDelegate, GCHandle.ToIntPtr(gch)));

            IntPtr propertyHandle = IntPtr.Zero;
            ThrowIfFail(Internal.Recognizer.recognizer_get_property_bag(recoHandle, out propertyHandle));
            Properties = new PropertyCollection(propertyHandle);
        }

        /// <summary>
        /// Gets the language name that was set when the recognizer was created.
        /// </summary>
        public string SpeechRecognitionLanguage
        {
            get
            {
                return Properties.GetProperty(PropertyId.SpeechServiceConnection_RecoLanguage);
            }
        }

        /// <summary>
        /// Gets target languages for translation that were set when the recognizer was created.
        /// The language is specified in BCP-47 format. The translation will provide translated text for each of language.
        /// </summary>
        public ReadOnlyCollection<string> TargetLanguages
        {
            get
            {
                var plainStr = Properties.GetProperty(PropertyId.SpeechServiceConnection_TranslationToLanguages);
                return new ReadOnlyCollection<string>(plainStr.Split(','));
            }
        }

        /// <summary>
        /// Gets the name of output voice if speech synthesis is used.
        /// </summary>
        public string VoiceName
        {
            get
            {
                return Properties.GetProperty(PropertyId.SpeechServiceConnection_TranslationVoice);
            }
        }

        /// <summary>
        /// The collection of properties and their values defined for this <see cref="TranslationRecognizer"/>.
        /// Note: The property collection is only valid until the recognizer owning this Properties is disposed or finalized.
        /// </summary>
        public PropertyCollection Properties { get; internal set; }

        /// <summary>
        /// Gets/sets authorization token used to communicate with the service.
        /// Note: The caller needs to ensure that the authorization token is valid. Before the authorization token
        /// expires, the caller needs to refresh it by calling this setter with a new valid token.
        /// Otherwise, the recognizer will encounter errors during recognition.
        /// </summary>
        public string AuthorizationToken
        {
            get
            {
                return Properties.GetProperty(PropertyId.SpeechServiceAuthorization_Token, string.Empty);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Properties.SetProperty(PropertyId.SpeechServiceAuthorization_Token, value);
            }
        }

        /// <summary>
        /// Starts speech translation, and returns after a single utterance is recognized. The end of a
        /// single utterance is determined by listening for silence at the end or until a maximum of 15
        /// seconds of audio is processed.  The task returns the recognition text as result.
        /// Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
        /// shot recognition like command or query.
        /// For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
        /// </summary>
        /// <returns>A task representing the recognition operation. The task returns a value of <see cref="TranslationRecognitionResult"/> </returns>
        /// <example>
        /// Create a translation recognizer, get and print the recognition result
        /// <code>
        /// public async Task TranslationSingleShotRecognitionAsync()
        /// {
        ///     // Creates an instance of a speech translation config with specified subscription key and service region. 
        ///     // Replace with your own subscription key and service region (e.g., "westus").
        ///     var config = SpeechTranslationConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
        ///
        ///     string fromLanguage = "en-US";
        ///     config.SpeechRecognitionLanguage = fromLanguage;
        ///     config.AddTargetLanguage("de");
        ///
        ///     // Creates a translation recognizer.
        ///     using (var recognizer = new TranslationRecognizer(config))
        ///     {
        ///         // Starts recognizing.
        ///         Console.WriteLine("Say something...");
        ///
        ///         // Starts translation recognition, and returns after a single utterance is recognized. The end of a
        ///         // single utterance is determined by listening for silence at the end or until a maximum of 15
        ///         // seconds of audio is processed. The task returns the recognized text as well as the translation.
        ///         // Note: Since RecognizeOnceAsync() returns only a single utterance, it is suitable only for single
        ///         // shot recognition like command or query.
        ///         // For long-running multi-utterance recognition, use StartContinuousRecognitionAsync() instead.
        ///         var result = await recognizer.RecognizeOnceAsync();
        ///
        ///         if (result.Reason == ResultReason.TranslatedSpeech)
        ///         {
        ///             Console.WriteLine($"\nFinal result: Reason: {result.Reason.ToString()}, recognized text: {result.Text}.");
        ///             foreach (var element in result.Translations)
        ///             {
        ///                 Console.WriteLine($"    TRANSLATING into '{element.Key}': {element.Value}");
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public Task<TranslationRecognitionResult> RecognizeOnceAsync()
        {
            return Task.Run(() =>
            {
                TranslationRecognitionResult result = null;
                base.DoAsyncRecognitionAction(() => result = new TranslationRecognitionResult(RecognizeOnce()));
                return result;
            });
        }

        /// <summary>
        /// Starts recognition and translation on a continous audio stream, until StopContinuousRecognitionAsync() is called.
        /// User must subscribe to events to receive translation results.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartContinuousRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(StartContinuousRecognition);
            });
        }

        /// <summary>
        /// Stops continuous recognition and translation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the translation.</returns>
        public Task StopContinuousRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(StopContinuousRecognition);
            });
        }

        /// <summary>
        /// Starts speech recognition on a continuous audio stream with keyword spotting, until StopKeywordRecognitionAsync() is called.
        /// User must subscribe to events to receive recognition results.
        /// </summary>
        /// <param name="model">The keyword recognition model that specifies the keyword to be recognized.</param>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartKeywordRecognitionAsync(KeywordRecognitionModel model)
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(() => StartKeywordRecognition(model));
            });
        }

        /// <summary>
        /// Stops continuous speech recognition with keyword spotting.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the recognition.</returns>
        public Task StopKeywordRecognitionAsync()
        {
            return Task.Run(() =>
            {
                base.DoAsyncRecognitionAction(StopKeywordRecognition);
            });
        }

        ~TranslationRecognizer()
        {
            isDisposing = true;
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                // This will make Properties unaccessible.
                Properties.Close();
            }

            if (recoHandle != null)
            {
                LogErrorIfFail(Internal.Recognizer.recognizer_recognizing_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_recognized_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_canceled_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.translator_synthesizing_audio_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_session_started_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_session_stopped_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_speech_start_detected_set_callback(recoHandle, null, IntPtr.Zero));
                LogErrorIfFail(Internal.Recognizer.recognizer_speech_end_detected_set_callback(recoHandle, null, IntPtr.Zero));
            }

            recognizingCallbackDelegate = null;
            recognizedCallbackDelegate = null;
            canceledCallbackDelegate = null;
            translationSynthesisCallbackDelegate = null;

            base.Dispose(disposing);
        }

        private CallbackFunctionDelegate recognizingCallbackDelegate;
        private CallbackFunctionDelegate recognizedCallbackDelegate;
        private CallbackFunctionDelegate canceledCallbackDelegate;
        private CallbackFunctionDelegate translationSynthesisCallbackDelegate;

        private readonly Audio.AudioConfig audioConfig;

        // Defines private methods to raise a C# event for intermediate/final result when a corresponding callback is invoked by the native layer.
        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Recognizing(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                var recognizer = InteropSafeHandle.GetObjectFromWeakHandle<TranslationRecognizer>(pvContext);
                if (recognizer == null || recognizer.isDisposing)
                {
                    return;
                }
                var resultEventArg = new TranslationRecognitionEventArgs(hevent);
                recognizer.Recognizing?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                LogError(Internal.SpxError.InvalidHandle);
            }
        }

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Recognized(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                var recognizer = InteropSafeHandle.GetObjectFromWeakHandle<TranslationRecognizer>(pvContext);
                if (recognizer == null || recognizer.isDisposing)
                {
                    return;
                }
                var resultEventArg = new TranslationRecognitionEventArgs(hevent);
                recognizer.Recognized?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                LogError(Internal.SpxError.InvalidHandle);
            }
        }

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_Canceled(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                var recognizer = InteropSafeHandle.GetObjectFromWeakHandle<TranslationRecognizer>(pvContext);
                if (recognizer == null || recognizer.isDisposing)
                {
                    return;
                }
                var resultEventArg = new TranslationRecognitionCanceledEventArgs(hevent);
                recognizer.Canceled?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                LogError(Internal.SpxError.InvalidHandle);
            }
        }

        [Internal.MonoPInvokeCallback]
        private static void FireEvent_SynthesisResult(IntPtr hreco, IntPtr hevent, IntPtr pvContext)
        {
            try
            {
                var recognizer = InteropSafeHandle.GetObjectFromWeakHandle<TranslationRecognizer>(pvContext);
                if (recognizer == null || recognizer.isDisposing)
                {
                    return;
                }
                var resultEventArg = new TranslationSynthesisEventArgs(hevent);
                recognizer.Synthesizing?.Invoke(recognizer, resultEventArg);
            }
            catch (InvalidOperationException)
            {
                LogError(Internal.SpxError.InvalidHandle);
            }
        }
    }
}
