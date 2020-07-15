﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using POpusCodec.Enums;
using System.Runtime.InteropServices;

namespace POpusCodec
{
    public class OpusDecoder<T> : IDisposable
    {
        private const bool UseInbandFEC = true;

        private bool TisFloat;
        private int sizeofT;
        
        private IntPtr _handle = IntPtr.Zero;
        private const int MaxFrameSize = 5760;

        private int _channelCount;

        private static readonly T[] EmptyBuffer = new T[] { };

        private Bandwidth? _previousPacketBandwidth = null;

        public Bandwidth? PreviousPacketBandwidth
        {
            get
            {
                return _previousPacketBandwidth;
            }
        }

        public OpusDecoder(SamplingRate outputSamplingRateHz, Channels numChannels)
        {
            TisFloat = default(T) is float;
            sizeofT = Marshal.SizeOf(default(T));

            if ((outputSamplingRateHz != SamplingRate.Sampling08000)
                && (outputSamplingRateHz != SamplingRate.Sampling12000)
                && (outputSamplingRateHz != SamplingRate.Sampling16000)
                && (outputSamplingRateHz != SamplingRate.Sampling24000)
                && (outputSamplingRateHz != SamplingRate.Sampling48000))
            {
                throw new ArgumentOutOfRangeException("outputSamplingRateHz", "Must use one of the pre-defined sampling rates (" + outputSamplingRateHz + ")");
            }
            if ((numChannels != Channels.Mono)
                && (numChannels != Channels.Stereo))
            {
                throw new ArgumentOutOfRangeException("numChannels", "Must be Mono or Stereo");
            }

            _channelCount = (int)numChannels;
            _handle = Wrapper.opus_decoder_create(outputSamplingRateHz, numChannels);

            if (_handle == IntPtr.Zero)
            {
                throw new OpusException(OpusStatusCode.AllocFail, "Memory was not allocated for the encoder");
            }
        }
        
        private T[] buffer; // allocated for exactly 1 frame size as first valid frame received
        private byte[] prevPacketData; // null if previous packet is invalid

        // pass null to indicate packet loss
        public T[] DecodePacket(byte[] packetData)
        {
            if (this.buffer == null && packetData == null)
            {
                return EmptyBuffer;
            }
            
            int numSamplesDecoded = 0;

            T[] buf;
            if (this.buffer == null)
            {
                // on the first call we don't know frame size, use temporal buffer of maximal length
                buf = new T[MaxFrameSize * _channelCount];                
            }
            else
            {
                buf = this.buffer;
            }

            bool packetInvalid = false;
            if (packetData == null)
            {
                packetInvalid = true;
            }
            else
            {
                int bandwidth = Wrapper.opus_packet_get_bandwidth(packetData);
                packetInvalid = bandwidth == (int)OpusStatusCode.InvalidPacket;
            }

            int fec = 0;
            byte[] pd = packetData;
            // if !UseInbandFEC, decode or conceal current frame
            if (UseInbandFEC)
            {
                if (prevPacketData == null)
                {
                    if (packetInvalid)
                    {
                        // no fec data, conceal previous frame
                        fec = 0;
                        pd = null;

                        //UnityEngine.Debug.Log("======================= Conceal");
                    }
                    else
                    {
                        // error correct previous frame with the help of the current
                        fec = 1;
                        pd = packetData;

                        //UnityEngine.Debug.Log("======================= FEC");
                    }
                }
                else
                {
                    // decode previous frame
                    fec = 0;
                    pd = prevPacketData;
                }
            }

            numSamplesDecoded = TisFloat ? 
                Wrapper.opus_decode(_handle, pd, buf as float[], fec, _channelCount) : 
                Wrapper.opus_decode(_handle, pd, buf as short[], fec, _channelCount);

            prevPacketData = packetInvalid ? null : packetData;

            if (numSamplesDecoded == 0)
                return EmptyBuffer;
            
            if (this.buffer == null)
            {
                if (pd == null || fec == 1)
                {
                    // wait for regular valid frame to imitialize the size
                    return EmptyBuffer;
                }
                // now that we know the frame size, allocate the buffer and copy data from temporal buffer
                this.buffer = new T[numSamplesDecoded * _channelCount];
                Buffer.BlockCopy(buf, 0, this.buffer, 0, numSamplesDecoded * sizeofT);
            }
            return this.buffer;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Wrapper.opus_decoder_destroy(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
