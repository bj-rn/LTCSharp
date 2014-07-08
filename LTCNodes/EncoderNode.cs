using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
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
        class EncodeInstance : IWaveProvider
        {
            LTCSharp.Encoder FEncoder;            

            public EncodeInstance(double SampleRate, int fps)
            {
                FEncoder = new LTCSharp.Encoder(SampleRate,
                    fps, 
                    LTCSharp.TVStandard.TV525_60i, 
                    LTCSharp.BGFlags.NONE);
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                lock (FEncoder)
                {
                    //Console.Write("bip");
                    FEncoder.encodeFrame();
                    int size = FEncoder.getBuffer(buffer, offset);
                    //Console.WriteLine(size);
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

        [Input("Input")]
        IDiffSpread<Timecode> FInTimecode;

        //[Input("Device")]
        //IDiffSpread<MMDevice> FInDevice;

        //[Input("Channel Count", DefaultValue = 2)]
        //IDiffSpread<uint> FInChannels;

        //[Input("Channel Index")]
        //IDiffSpread<uint> FInChannel;

        //[Output("Timecode")]
        //ISpread<LTCSharp.Timecode> FOutTimecode;

        [Output("Status")]
        ISpread<string> FOutStatus;
#pragma warning restore

        Spread<EncodeInstance> FInstances = new Spread<EncodeInstance>(0);
        Spread<WaveOut> FWaveOuts = new Spread<WaveOut>(0);
        bool firstStart = true;

        public void Evaluate(int SpreadMax)
        {
            // do the following OnStart
            if (firstStart || FInstances.SliceCount != SpreadMax)
            {
                FInstances.SliceCount = 0;
                FOutStatus.SliceCount = SpreadMax;

                foreach (var wave in FWaveOuts)
                {
                    if (wave != null)
                    {
                        wave.Stop();
                        wave.Dispose();
                    }
                }

                for (int i = 0; i < SpreadMax; i++)
                {
                    try
                    {
                        var waveOut = new WaveOut();
                        FWaveOuts.Add(waveOut);

                        var instance = new EncodeInstance(44100, 30);
                        FInstances.Add(instance);

                        waveOut.Init(instance);
                        waveOut.Play();

                        FOutStatus[i] = "OK";
                    }
                    catch (Exception e)
                    {
                        FInstances.Add(null);
                        FOutStatus[i] = e.Message;
                    }
                }
            }

            for (int i = 0; i < FInstances.SliceCount; i++)
            {
                if (FInstances[i] != null)
                {
                    if (FInTimecode[i] != null)
                    {
                        if (FWaveOuts[i].PlaybackState == PlaybackState.Paused)
                            FWaveOuts[i].Resume();

                        try
                        {
                            float vol = FWaveOuts[i].Volume;
                            this.FInstances[i].SetTimecode(FInTimecode[i]);
                        }
                        catch (Exception e)
                        {
                            FOutStatus[i] = e.Message;
                        }
                    }
                    else
                    {
                        FWaveOuts[i].Pause();
                        FOutStatus[i] = "You have to provide a Timecode";
                    }
                }
                //else
                //{
                //    FOutTimecode[i] = null;
                //}
            }
        }

        public void Dispose()
        {
            foreach (var wave in FWaveOuts)
            {
                if (wave != null)
                {
                    wave.Stop();
                    wave.Dispose();
                }
            }
        }

    }
}
