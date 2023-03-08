using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;
using MusicXml;
using MusicXml.Domain;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Format {
    public static class MusicXML {

        //TODO:Timewise support
        public const string musicXMLNameSpace = "http://www.musicxml.org/dtds/partwise.dtd";

        static public UProject LoadProject(string file) {
            //TODO: Tempo, Text encoding
            var project = new UProject();
            Ustx.AddDefaultExpressions(project);
            var score = MusicXmlParser.GetScore(file);
            var parts = ParseParts(score, project);
            foreach (var part in parts) {
                var track = new UTrack();
                track.TrackNo = project.tracks.Count;
                part.trackNo = track.TrackNo;
                part.AfterLoad(project, track);
                project.tracks.Add(track);
                project.parts.Add(part);
            }
            return project;
        }
        
        static public List<UVoicePart> Load(string file, UProject project) {
            return ParseParts(MusicXmlParser.GetScore(file), project);
        }

        static public List<UVoicePart> ParseParts(Score score, UProject project) {
            return score.Parts.Select(p => parsePart(p, project)).ToList();
        }

        static public UVoicePart parsePart(MusicXml.Domain.Part part, UProject project) {
            string defaultLyric = NotePresets.Default.DefaultLyric;
            int tick = 0;
            int divisions = 480;
            var uPart = new UVoicePart();
            List<UNote> uNotes = new List<UNote>();
            foreach (var m in part.Measures) {
                if (m.Attributes != null) {
                    divisions = m.Attributes.Divisions;
                }
                foreach (var me in m.MeasureElements.Where(me => me != null)) {
                    switch (me.Type) {
                        case MeasureElementType.Note:
                            var note = (MusicXml.Domain.Note)(me.Element);
                            int duration = note.Duration * 480 / divisions;
                            if (!note.IsRest) {
                                int tone = (note.Pitch.Octave + 1) * 12 
                                    + MusicMath.NameInOctave[note.Pitch.Step.ToString()] 
                                    + note.Pitch.Alter;
                                string lyric;
                                if (note.Lyric.Text != null) {
                                    lyric = note.Lyric.Text;
                                    if (lyric == "-") {
                                        lyric = "+";
                                    }
                                } else {
                                    //TODO:slur
                                    //Blocked by MusicXml.Net
                                    lyric = "+";
                                }
                                var uNote = project.CreateNote(tone, tick, duration);
                                uNote.lyric = lyric;
                                uPart.notes.Add(uNote);
                            }
                            tick += duration;
                            break;
                        case MeasureElementType.Backup:
                            tick -= ((Backup)me.Element).Duration * 480 / divisions;
                            break;
                        case MeasureElementType.Forward:
                            tick += ((Forward)me.Element).Duration * 480 / divisions;
                            break;
                        default: break;
                    }
                }
            }
            return uPart;
        }
    }
}
