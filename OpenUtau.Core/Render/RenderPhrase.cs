using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using K4os.Hash.xxHash;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Render {
    public class RenderNote {
        public readonly string lyric;
        public readonly int position;
        public readonly int duration;
        public readonly int tone;

        public RenderNote(UNote note) {
            lyric = note.lyric;
            position = note.position;
            duration = note.duration;
            tone = note.tone;
        }
    }

    public class RenderPhone {
        public readonly int position;
        public readonly int duration;
        public readonly int leading;
        public readonly string phoneme;
        public readonly int tone;
        public readonly int noteIndex;

        // classic args
        public readonly string resampler;
        public readonly Tuple<string, int?>[] flags;
        public readonly float volume;
        public readonly float velocity;
        public readonly float modulation;
        public readonly float preutterMs;
        public readonly Vector2[] envelope;

        public readonly UOto oto;
        public readonly ulong hash;

        internal RenderPhone(UProject project, UTrack track, UVoicePart part, UNote note, UPhoneme phoneme) {
            position = phoneme.position;
            duration = phoneme.Duration;
            leading = (int)Math.Round(project.MillisecondToTick(phoneme.preutter) / 5.0) * 5; // TODO
            this.phoneme = phoneme.phoneme;
            tone = note.tone;

            int eng = (int)phoneme.GetExpression(project, track, Format.Ustx.ENG).Item1;
            if (project.expressions.TryGetValue(Format.Ustx.ENG, out var descriptor)) {
                if (eng < 0 || eng >= descriptor.options.Length) {
                    eng = 0;
                }
                resampler = descriptor.options[eng];
                if (string.IsNullOrEmpty(resampler)) {
                    resampler = Util.Preferences.Default.Resampler;
                }
            }
            flags = phoneme.GetResamplerFlags(project, track);
            volume = phoneme.GetExpression(project, track, Format.Ustx.VOL).Item1 * 0.01f;
            velocity = phoneme.GetExpression(project, track, Format.Ustx.VEL).Item1 * 0.01f;
            modulation = phoneme.GetExpression(project, track, Format.Ustx.MOD).Item1 * 0.01f;
            preutterMs = phoneme.preutter;
            envelope = phoneme.envelope.data.ToArray();

            oto = phoneme.oto;
            hash = Hash();
        }

        private ulong Hash() {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream)) {
                    writer.Write(duration);
                    writer.Write(phoneme ?? "");
                    writer.Write(tone);

                    writer.Write(resampler ?? "");
                    foreach (var flag in flags) {
                        writer.Write(flag.Item1);
                        if (flag.Item2.HasValue) {
                            writer.Write(flag.Item2.Value);
                        }
                    }
                    writer.Write(volume);
                    writer.Write(velocity);
                    writer.Write(modulation);
                    writer.Write(preutterMs);
                    foreach (var point in envelope) {
                        writer.Write(point.X);
                        writer.Write(point.Y);
                    }
                    return XXH64.DigestOf(stream.ToArray());
                }
            }
        }
    }

    public class RenderPhrase {
        public readonly string singerId;
        public readonly USinger singer;
        public readonly int position;
        public readonly double tempo;
        public readonly double tickToMs;
        public readonly RenderNote[] notes;
        public readonly RenderPhone[] phones;
        public readonly int pitchStart;
        public readonly float[] pitches;//音高曲线
        public readonly float[] pitchesBeforeDeviation;
        public readonly float[] dynamics;
        public readonly float[] gender;
        public readonly float[] breathiness;
        public readonly float[] toneShift;
        public readonly float[] tension;
        public readonly float[] voicing;
        public readonly ulong hash;

        internal readonly IRenderer renderer;
        //渲染一段音素，可能是一个或多个
        internal RenderPhrase(UProject project, UTrack track, UVoicePart part, IEnumerable<UPhoneme> phonemes) {
            var uNotes = new List<UNote>();//所涉及的音符
            uNotes.Add(phonemes.First().Parent);//首个音素所属音符。这段代码将所渲染音素所涉及的音符全部加入uNotes
            var endNote = phonemes.Last().Parent;
            while (endNote.Next != null && endNote.Next.Extends != null) {
                endNote = endNote.Next;
            }
            while (uNotes.Last() != endNote) {
                uNotes.Add(uNotes.Last().Next);
            }
            var tail = uNotes.Last();//所涉及的最后一个音符
            var next = tail.Next;//所涉及的最后一个音符的下一个音符（未被涉及）
            while (next != null && next.Extends == tail) {
                uNotes.Add(next);
                next = next.Next;
            }
            notes = uNotes
                .Select(n => new RenderNote(n))
                .ToArray();
            phones = phonemes
                .Select(p => new RenderPhone(project, track, part, p.Parent, p))
                .ToArray();

            singerId = track.Singer.Id;
            singer = track.Singer;
            renderer = track.Renderer;
            position = part.position;
            tempo = project.bpm;
            tickToMs = 60000.0 / project.bpm * project.beatUnit / 4 / project.resolution;

            const int pitchInterval = 5;//每5tick一个音高点
            pitchStart = phones[0].position - phones[0].leading;//音高线起点：开头音素的位置-提前量，即开头音素的最终起点
            pitches = new float[(phones.Last().position + phones.Last().duration - pitchStart) / pitchInterval + 1];//音高线长度。音高线终点为结尾音素的末端
            int index = 0;
            foreach (var note in uNotes) {
                while (pitchStart + index * pitchInterval < note.End && index < pitches.Length) {
                    pitches[index] = note.tone * 100;
                    index++;
                }//基础音高线为阶梯，只管当前处于哪个音符
            }
            index = Math.Max(1, index);
            while (index < pitches.Length) {
                pitches[index] = pitches[index - 1];//结尾如果还有多余的地方，就用最后一个音符的音高填充
                index++;
            }
            foreach (var note in uNotes) {//对每个音符
                if (note.vibrato.length <= 0) {//如果音符的颤音长度<=0，则无颤音。颤音长度按毫秒存储
                    continue;
                }
                int startIndex = Math.Max(0, (int)Math.Ceiling((float)(note.position - pitchStart) / pitchInterval));//音符起点在采样音高线上的x坐标
                int endIndex = Math.Min(pitches.Length, (note.End - pitchStart) / pitchInterval);//音符终点在采样音高线上的x坐标
                for (int i = startIndex; i < endIndex; ++i) {
                    float nPos = (float)(pitchStart + i * pitchInterval - note.position) / note.duration;//音符长度，单位为5tick
                    float nPeriod = (float)project.MillisecondToTick(note.vibrato.period) / note.duration;//颤音长度，单位为5tick
                    var point = note.vibrato.Evaluate(nPos, nPeriod, note);//将音符长度颤音长度代入进去，求出带颤音的音高线
                    pitches[i] = point.Y * 100;
                }
            }
            foreach (var note in uNotes) {//对每个音符
                var pitchPoints = note.pitch.data//音高控制点
                    .Select(point => new PitchPoint(//OpenUTAU的控制点按毫秒存储（这个设计会导致修改曲速时出现混乱），这里先转成tick
                        project.MillisecondToTick(point.X) + note.position,
                        point.Y * 10 + note.tone * 100,
                        point.shape))
                    .ToList();
                if (pitchPoints.Count == 0) {//如果没有控制点，则默认台阶形
                    pitchPoints.Add(new PitchPoint(note.position, note.tone * 100));
                    pitchPoints.Add(new PitchPoint(note.End, note.tone * 100));
                }
                if (note == uNotes.First() && pitchPoints[0].X > pitchStart) {
                    pitchPoints.Insert(0, new PitchPoint(pitchStart, pitchPoints[0].Y));//如果整个段落开头有控制点没覆盖到的地方（以音素开头为准），则向前水平延伸
                } else if (pitchPoints[0].X > note.position) {
                    pitchPoints.Insert(0, new PitchPoint(note.position, pitchPoints[0].Y));//对于其他音符，则以卡拍点为准
                }
                if (pitchPoints.Last().X < note.End) {
                    pitchPoints.Add(new PitchPoint(note.End, pitchPoints.Last().Y));//如果整个段落结尾有控制点没覆盖到的地方，则向后水平延伸
                }
                PitchPoint lastPoint = pitchPoints[0];//现在lastpoint是第一个控制点
                index = Math.Max(0, (int)((lastPoint.X - pitchStart) / pitchInterval));//起点在采样音高线上的x坐标，以5tick为单位。如果第一个控制点在0前面，就从0开始，否则从第一个控制点开始
                foreach (var point in pitchPoints.Skip(1)) {//对每一段曲线
                    int x = pitchStart + index * pitchInterval;//起点在工程中的x坐标
                    while (x < point.X && index < pitches.Length) {//遍历采样音高点
                        float pitch = (float)MusicMath.InterpolateShape(lastPoint.X, point.X, lastPoint.Y, point.Y, x, lastPoint.shape);//绝对音高。插值，正式将控制点转化为曲线！
                        float basePitch = note.Prev != null && x < note.Prev.End
                            ? note.Prev.tone * 100
                            : note.tone * 100;//台阶基础音高
                        pitches[index] += pitch - basePitch;//锚点音高比基础音高高了多少
                        index++;
                        x += pitchInterval;
                    }
                    lastPoint = point;
                }
            }

            pitchesBeforeDeviation = pitches.ToArray();
            var curve = part.curves.FirstOrDefault(c => c.abbr == Format.Ustx.PITD);//PITD为手绘音高线差值。这里从ustx工程中尝试调取该参数
            if (curve != null && !curve.IsEmpty) {//如果参数存在
                for (int i = 0; i < pitches.Length; ++i) {
                    pitches[i] += curve.Sample(pitchStart + i * pitchInterval);//每个点加上PITD的值
                }
            }

            dynamics = SampleCurve(part, Format.Ustx.DYN, pitchStart, pitches.Length,
                (x, c) => x == c.descriptor.min
                    ? 0
                    : (float)MusicMath.DecibelToLinear(x * 0.1));
            toneShift = SampleCurve(part, Format.Ustx.SHFC, pitchStart, pitches.Length, (x, _) => x);
            gender = SampleCurve(part, Format.Ustx.GENC, pitchStart, pitches.Length, (x, _) => x);
            tension = SampleCurve(part, Format.Ustx.TENC, pitchStart, pitches.Length, (x, _) => x);
            breathiness = SampleCurve(part, Format.Ustx.BREC, pitchStart, pitches.Length, (x, _) => x);
            voicing = SampleCurve(part, Format.Ustx.VOIC, pitchStart, pitches.Length, (x, _) => x);

            hash = Hash();
        }

        private static float[] SampleCurve(UVoicePart part, string abbr, int start, int length, Func<float, UCurve, float> convert) {
            const int interval = 5;
            var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
            if (curve == null || curve.IsEmptyBetween(
                start, start + (length - 1) * interval, (int)curve.descriptor.defaultValue)) {
                return null;
            }
            var result = new float[length];
            for (int i = 0; i < length; ++i) {
                result[i] = convert(curve.Sample(start + i * interval), curve);
            }
            return result;
        }

        private ulong Hash() {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream)) {
                    writer.Write(singerId);
                    writer.Write(tempo);
                    writer.Write(tickToMs);
                    foreach (var phone in phones) {
                        writer.Write(phone.hash);
                    }
                    foreach (var array in new float[][] { pitches, dynamics, gender, breathiness, toneShift, tension, voicing }) {
                        if (array == null) {
                            writer.Write("null");
                        } else {
                            foreach (var v in array) {
                                writer.Write(v);
                            }
                        }
                    }
                    return XXH64.DigestOf(stream.ToArray());
                }
            }
        }

        public static List<RenderPhrase> FromPart(UProject project, UTrack track, UVoicePart part) {
            var phrases = new List<RenderPhrase>();
            var phonemes = part.phonemes
                .Where(phoneme => !phoneme.Error)
                .ToList();
            if (phonemes.Count == 0) {
                return phrases;
            }
            var phrasePhonemes = new List<UPhoneme>() { phonemes[0] };
            for (int i = 1; i < phonemes.Count; ++i) {
                if (phonemes[i - 1].End != phonemes[i].position) {
                    phrases.Add(new RenderPhrase(project, track, part, phrasePhonemes));
                    phrasePhonemes.Clear();
                }
                phrasePhonemes.Add(phonemes[i]);
            }
            if (phrasePhonemes.Count > 0) {
                phrases.Add(new RenderPhrase(project, track, part, phrasePhonemes));
                phrasePhonemes.Clear();
            }
            return phrases;
        }
    }
}
