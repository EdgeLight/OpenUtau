﻿using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Classic {
    public static class ClassicSingerLoader {
        static USinger AdjustSingerType(Voicebank v) {
            switch (v.SingerType) {
                case USingerType.Enunu:
                    return new Core.Enunu.EnunuSinger(v) as USinger;
                case USingerType.DiffSinger:
                    return new Core.DiffSinger.DiffSingerSinger(v) as USinger;
                default:
                    return new ClassicSinger(v) as USinger;
            }
        }
        public static IEnumerable<USinger> FindAllSingers() {
            List<USinger> singers = new List<USinger>();
            foreach (var path in new string[] {
                PathManager.Inst.SingersPathOld,
                PathManager.Inst.SingersPath,
                PathManager.Inst.AdditionalSingersPath,
            }) {
                var loader = new VoicebankLoader(path);
                singers.AddRange(loader.SearchAll()
                    .Select(AdjustSingerType));
            }
            return singers;
        }
    }
}
