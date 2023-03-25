using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NumSharp;
using NumSharp.Generic;

namespace OpenUtau.Plugin.Builtin.EnunuOnnx {
    public class ScalerLine {
        public float xmin;
        public float scale;

        public void inverse_transform(IList<float> xs) {
            //In-place transform a bunch of vectors
            for (int i = 0; i < xs.Count; i++) {
                xs[i] = xs[i] / this.scale + this.xmin;
            }
        }
    }

    public class Scaler {
        public NDArray xmins;
        public NDArray scales;

        public int Count => xmins.shape[0];

        public Scaler(NDArray xmins, NDArray scales){
            if(xmins.shape.Length>1 || scales.shape.Length>1){
                throw new Exception("xmins and scales must be 1D");
            }
            if(xmins.shape[0] != scales.shape[0]){
                throw new Exception("xmins and scales must have the same length");
            }
            this.xmins = xmins;
            this.scales = scales;
        }

        public static Scaler load(string path, Encoding encoding = null) {
            //Encoding is UTF-8 by default
            if (encoding == null) {
                encoding = Encoding.UTF8;
            }
            var ScalerLines = JsonConvert.DeserializeObject<List<ScalerLine>>(
                File.ReadAllText(path, encoding));
            return new Scaler(
                np.array<float>(ScalerLines.Select(line=>line.xmin).ToArray()).AsGeneric<float>(),
                np.array<float>(ScalerLines.Select(line=>line.scale).ToArray()).AsGeneric<float>()
            );
        }

        public void transform(IList<float> x) {
            //In-place transform a vector
            for(int i = 0; i < this.Count; i++) {
                x[i] = (x[i] - xmins[i]) * scales[i];
            }
        }
        
        public void transform(IEnumerable<IList<float>> xs) {
            //In-place transform a bunch of vectors
            foreach(IList<float> x in xs) {
                transform(x);
            }
        }

        /*public float[] transformed(IEnumerable<float> x) {
            //transform a vector into a new vector
            return Enumerable.Zip(this, x, (scalerLine, xLine) => (xLine - scalerLine.xmin) * scalerLine.scale).ToArray();
        }*/

        public NDArray transformed(NDArray xs) {
            //transform a bunch of vectors into new ones
            return (xs-xmins) * scales;
        }


        public void inverse_transform(IList<float> x) {
            for (int i = 0; i < this.Count; i++) {
                x[i] = x[i] / scales[i] + xmins[i];
            }
        }

        /*public void inverse_transform(IEnumerable<IList<float>> xs) {
            //In-place transform a bunch of vectors
            foreach (IList<float> x in xs) {
                inverse_transform(x);
            }
        }*/

        public NDArray inverse_transformed(NDArray xs) {
            //In-place transform a bunch of vectors
            return xs / scales + xmins;
        }
    }
}
