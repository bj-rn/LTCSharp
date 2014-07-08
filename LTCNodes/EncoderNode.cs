using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.Composition;

using NAudio.CoreAudioApi;
using NAudio.Wave;

using VVVV.Hosting.IO;
using VVVV.PluginInterfaces.V2;

using LTCSharp;

namespace VVVV.Nodes.LTC
{
    #region PluginInfo
    [PluginInfo(Name = "Encoder", 
                Category = "Timecode", 
                Version = "LTC", 
                Help = "Encode audio as LTC timecode", 
                Tags = "", 
                Author = "sebl", 
                AutoEvaluate = true)]
    #endregion PluginInfo

    public class EncoderNode : IPluginEvaluate, IDisposable
    {
        class LTCEncoder : IWaveProvider
        {
            LTCSharp.Encoder FEncoder;

            public LTCEncoder(double SampleRate, int fps, TVStandard tvstandard = TVStandard.TV525_60i)
            {
                FEncoder = new LTCSharp.Encoder(SampleRate,
                    fps, 
                    tvstandard, 
                    LTCSharp.BGFlags.NONE);
            }

            public void Dispose() 
            {
                FEncoder.Dispose();
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                lock (FEncoder)
                {
                    FEncoder.encodeFrame();
                    int size = FEncoder.getBuffer(buffer, offset);
                    return size;
                }
            }

            public LTCSharp.Encoder Encoder
            {
                get
                {
                    return this.FEncoder;
                }
            }

            public WaveFormat WaveFormat
            {
                get
                {
                    return new WaveFormat(48000, 1);
                }
            }


            public void SetTimecode(LTCSharp.Timecode timecode)
            {
                lock (FEncoder)
                {
                    FEncoder.setTimecode(timecode);
                }
            }
        }

        #pragma warning disable 649
        [Input("Timecode", IsSingle = true)]
        IDiffSpread<Timecode> FInTimecode;

        [Input("SampleRate", IsSingle = true, DefaultValue = 48000)]
        IDiffSpread<int> FSampleRate;

        [Input("FPS", IsSingle = true, DefaultValue = 60)]
        IDiffSpread<int> FFPS;

        [Input("TVStandards", DefaultEnumEntry = "TV525_60i", IsSingle = true)]
        IDiffSpread<TVStandard> FTVStandards;

        [Input("Volume", IsSingle = true, DefaultValue = 0.5, MinValue = 0, MaxValue = 1, Visibility = PinVisibility.OnlyInspector)]
        IDiffSpread<float> FVolume;

        [Input("Enable", IsSingle = true, DefaultBoolean = true)]
        ISpread<bool> FEnable;


        [Output("Status", IsSingle= true)]
        ISpread<string> FOutStatus;
        #pragma warning restore

        private WaveOut FWaveOut;
        private LTCEncoder FLTCEncoder;
        private bool FFirstFrame = true;


        [ImportingConstructor]
        public EncoderNode()
        {
        //    FOutStatus.SliceCount = 10;

        //    try
        //    {
        //        WaveOut FWaveOut = new WaveOut();
        //        LTCEncoder FLTCEncoder = new LTCEncoder(44100, 30);


        //        FWaveOut.Init(FLTCEncoder);

        //        FOutStatus[0] = "OK";
        //    }
        //    catch (Exception e)
        //    {
        //        FOutStatus[0] = e.Message;
        //    }

        }

        public void Dispose()
        {
            FWaveOut.Stop();
            FWaveOut.Dispose();
        }


        public void Evaluate(int SpreadMax)
        {
           
             for (int i = 0; i < SpreadMax; i++)
             {

                 if (FFirstFrame)
                 {
                     FFirstFrame = false;
                     FOutStatus.SliceCount = 1;

                     try
                     {
                         FWaveOut = new WaveOut();
                         FLTCEncoder = new LTCEncoder(48000, 60);

                         FWaveOut.Init(FLTCEncoder);

                         FOutStatus[0] = "OK";
                     }
                     catch (Exception e)
                     {
                         FOutStatus[0] = e.Message;
                     }


                 }


                 if (FSampleRate.IsChanged || FFPS.IsChanged || FTVStandards.IsChanged)
                 {

                     try
                     {
                         FWaveOut.Stop();
                         FLTCEncoder = null;

                         FLTCEncoder = new LTCEncoder(FSampleRate[0], FFPS[0], FTVStandards[0]);

                         FWaveOut.Init(FLTCEncoder);
                        
                     }
                     catch (Exception e)
                     {
                         FOutStatus[0] = e.Message;
                     }
                 
                 }


                if (FLTCEncoder != null)
                {

                    FOutStatus[i] = "OK";

                    if (FInTimecode[0] != null)
                    {

                        try
                        {
                            if (FEnable[0] == true)
                            {

                                FWaveOut.Play();
                            }
                            else
                            {
                                FWaveOut.Pause();
                            }

                            if (FVolume.IsChanged)
                            {
                                FWaveOut.Volume = FVolume[0];
                            }

                            FLTCEncoder.SetTimecode(FInTimecode[i]);
                        }
                        catch (Exception e)
                        {
                            FOutStatus[i] = e.Message;
                        }
                    }
                    else
                    {
                        FWaveOut.Pause();
                        FOutStatus[i] = "You have to provide a Timecode";
                    }
                }
              
            }
        }

    }
}
