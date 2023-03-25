using System.Linq;
using NumSharp;

//reference: https://github.com/r9y9/nnmnkwii/blob/master/nnmnkwii/preprocessing/f0.py

namespace OpenUtau.Plugin.Builtin.EnunuOnnx.nnmnkwii.preprocessing{
    public static class f0{
        public static NDArray interp1d(NDArray f0){
            int ndim = f0.ndim;
            //if len(f0) != f0.size:
            //    raise RuntimeError("1d array is only supported")
            var continuous_f0 = f0.flatten();
            var nonzero_indices = np.nonzero(continuous_f0)[0].ToArray<int>();
            
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
