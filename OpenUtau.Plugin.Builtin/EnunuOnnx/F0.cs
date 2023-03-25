using System;
using System.Linq;
using NumSharp;

//reference: https://github.com/r9y9/nnmnkwii/blob/master/nnmnkwii/preprocessing/f0.py

namespace OpenUtau.Plugin.Builtin.EnunuOnnx.nnmnkwii.preprocessing{
    public static class f0{
        public static NDArray interp1d(NDArray f0){
            if (f0.ndim > 1) {
                throw new Exception("only 1d array is supported");
            }
            var continuous_f0 = f0.flatten();
            var nonzero_indices = Enumerable.Range(0, f0.size)
                .Where(i => (float)(f0[i])>0)
                .ToArray();
            
            //Nothing to do
            if(nonzero_indices.Length<=0){
                return f0;
            }

            //Need this to insert continuous values for the first/end silence segments
            continuous_f0[0] = continuous_f0[nonzero_indices[0]];
            continuous_f0[-1] = continuous_f0[nonzero_indices[^1]];

            //interpolate
            Enumerable.Zip(
                nonzero_indices.Prepend(0),
                nonzero_indices.Append(continuous_f0.size-1),
                (a,b)=>{
                    if(b-a>1){
                        continuous_f0[new Slice(a,b+1)] = np.linspace(continuous_f0[a],continuous_f0[b],b-a+1);
                    }
                    return true;
                }
            ).Last();
            return continuous_f0;
        }
    }
}
