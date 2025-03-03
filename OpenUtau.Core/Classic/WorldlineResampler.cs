﻿using System.IO;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Classic {
    internal class WorldlineResampler : IResampler {
        public const string name = "worldline";
        public string FilePath { get; private set; }

        public WorldlineResampler() {
            string ext = OS.IsWindows() ? ".dll" : OS.IsMacOS() ? ".dylib" : ".so";
            FilePath = Path.Join(PathManager.Inst.RootPath, name + ext);
        }

        public float[] DoResampler(ResamplerItem item, ILogger logger) {
            return Worldline.Resample(item);
        }

        public string DoResamplerReturnsFile(ResamplerItem item, ILogger logger) {
            var samples = DoResampler(item, logger);
            var source = new WaveSource(0, 0, 0, 1);
            source.SetSamples(samples);
            lock (Renderers.GetCacheLock(item.outputFile)) {
                WaveFileWriter.CreateWaveFile16(item.outputFile, new ExportAdapter(source).ToMono(1, 0));
            }
            return item.outputFile;
        }

        public void CheckPermissions() { }

        //TODO: A list of flags supported by worldline resampler
        public ResamplerManifest Manifest { get; } = new ResamplerManifest();

        public bool SupportsFlag(string abbr) {
            return true;
        }

        public override string ToString() => name;
    }
}
