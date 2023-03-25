using NumSharp.Generic;
using NumSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using OpenUtau.Plugin.Builtin.EnunuOnnx.nnmnkwii.io.hts;
//reference: https://github.com/r9y9/nnmnkwii/blob/master/nnmnkwii/frontend/merlin.py

namespace OpenUtau.Plugin.Builtin.EnunuOnnx.nnmnkwii.frontend {
    public class merlin {
        //TODO:Should subphone_features be an enum?
        static Dictionary<string, int> frame_feature_size_dict = new Dictionary<string, int>
        {
            {"full",9},
            {"state_only",1 },
            {"frame_only",1 },
            {"uniform_state",2 },
            {"minimal_phoneme",3 },
            {"coarse_coding",4 },
        };

        public static int get_frame_feature_size(string subphone_features = "full") {
            if (subphone_features == null) {
                return 0;
            }
            subphone_features = subphone_features.Trim().ToLower();
            if (subphone_features == "none") {
                //TODO:raise ValueError("subphone_features = 'none' is deprecated, use None instead")
                throw new Exception("subphone_features = 'none' is deprecated, use None instead");
            }
            if (frame_feature_size_dict.TryGetValue(subphone_features, out var result)) {
                return result;
            } else {
                //TODO:raise ValueError("Unknown value for subphone_features: %s" % (subphone_features))
                throw new Exception($"Unknown value for subphone_features: {subphone_features}");
            }
        }

        public static NDArray<float> pattern_matching_binary(
            Dictionary<int, Tuple<string, List<Regex>>> binary_dict, string label) {
            int dict_size = binary_dict.Count;
            var lab_binary_vector = np.zeros<float>(new int[]{ dict_size }).AsGeneric<float>();
            foreach (int i in Enumerable.Range(0, dict_size)) {
                //ignored code: Always true
                //if isinstance(current_question_list, tuple):
                var current_question_list = binary_dict[i].Item2;
                var binary_flag = current_question_list
                    .Any(current_compiled => current_compiled.Match(label).Success) ? 1 : 0;
                lab_binary_vector[i] = binary_flag;
            }
            return lab_binary_vector;
        }

        public static NDArray<float> pattern_matching_continous_position(
            Dictionary<int, Tuple<string, Regex>> numeric_dict, string label) {
            int dict_size = numeric_dict.Count;
            var lab_continuous_vector = np.zeros<float>(new int[] { dict_size }).AsGeneric<float>();
            foreach (int i in Enumerable.Range(0, dict_size)) {
                //ignored code: Always true
                //if isinstance(current_compiled, tuple):

                var current_compiled = numeric_dict[i].Item2;
                //# NOTE: newer version returns tuple of (name, question)

                //ignore code:
                //if isinstance(current_compiled, tuple):
                //  current_compiled = current_compiled[1]
                float continuous_value;
                if (current_compiled.ToString().Contains("([-\\d]+)")) {
                    continuous_value = -50.0f;
                } else {
                    continuous_value = -1.0f;
                }

                var ms = current_compiled.Match(label);
                if (ms.Success) {
                    string note = ms.Groups[1].Value;
                    if (HTS.NameToTone(note)>0) {
                        continuous_value = HTS.NameToTone(note);
                    } else if (note.StartsWith("p")) {
                        continuous_value = int.Parse(note[1..]);
                    } else if (note.StartsWith("m")) {
                        continuous_value = -int.Parse(note[1..]);
                    } else if (float.TryParse(note, out float num)) {
                        continuous_value = num;
                    }
                    
                }
                lab_continuous_vector[i] = continuous_value;
            }
            return lab_continuous_vector;
        }

        public static NDArray load_labels_with_phone_alignment(
            HTSLabelFile hts_labels,
            Dictionary<int, Tuple<string, List<Regex>>> binary_dict,
            Dictionary<int, Tuple<string, Regex>> numeric_dict,
            string subphone_features = null,
            bool add_frame_features = false,
            int frame_shift = 50000
            ) {
            int dict_size = binary_dict.Count + numeric_dict.Count;
            int frame_feature_size = get_frame_feature_size(subphone_features);
            int featuresDim = frame_feature_size + dict_size;
            int phonemesCount;
            if (add_frame_features) {
                phonemesCount = hts_labels.num_frames();
            } else {
                phonemesCount = hts_labels.num_phones();
            }
            int label_feature_index = 0;

            //matrix size: dimx*dimension
            var label_feature_matrix = np.zeros<float>(phonemesCount, featuresDim);
            if (subphone_features == "coarse_coding") {
                throw new NotImplementedException();
                //TODO:compute_coarse_coding_features()
            }
            foreach (int phonemeId in Enumerable.Range(0, hts_labels.Count)) {
                var label = hts_labels[phonemeId];
                var frame_number = label.end_time / frame_shift - label.start_time / frame_shift;
                //label_binary_vector = pattern_matching_binary(binary_dict, full_label)
                var label_vector = pattern_matching_binary(binary_dict, label.context).astype(np.float32);

                var label_continuous_vector = pattern_matching_continous_position(numeric_dict, label.context);
                label_vector = np.concatenate(new NDArray[] { label_vector, label_continuous_vector });
                //label_vector.AddRange(label_continuous_vector);

                /*TODO:
                 if subphone_features == "coarse_coding":
                    cc_feat_matrix = extract_coarse_coding_features_relative(
                        cc_features, frame_number)
                 */
                if (add_frame_features) {
                    throw new NotImplementedException();
                    //TODO
                } else if (subphone_features == null) {
                    label_feature_matrix[phonemeId] = label_vector;
                }
            }
            //#omg
            //TODO
            /*
             if label_feature_index == 0:
            raise ValueError(
                "Combination of subphone_features and add_frame_features is not supported: {}, {}".format(
                    subphone_features, add_frame_features
                    ))
             */
            return label_feature_matrix;
        }

        public static NDArray linguistic_features(
            HTSLabelFile hts_labels,
            Dictionary<int, Tuple<string, List<Regex>>> binary_dict,
            Dictionary<int, Tuple<string, Regex>> numeric_dict,
            string subphone_features = null,
            bool add_frame_features = false,
            int frame_shift = 50000
            ) {
            if (hts_labels.is_state_alignment_label()) {
                throw new NotImplementedException();
                //TODO:load_labels_with_state_alignment
            } else {
                return load_labels_with_phone_alignment(
                    hts_labels,
                    binary_dict,
                    numeric_dict,
                    subphone_features,
                    add_frame_features,
                    frame_shift
                    );
            }
        }
    }
}
