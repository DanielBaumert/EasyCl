#ifdef cl_arm_printf
#pragma OPENCL EXTENSION cl_amd_printf : enable
#else
#pragma OPENCL EXTENSION cl_intel_printf : enable
#endif

#define SIZE 9

__constant sampler_t sampler = CLK_NORMALIZED_COORDS_FALSE |  CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;

inline int4 calcGrayScale(int4 rawPixel) {
	float channel1 = ((float)rawPixel.x) * 0.11f;// e.g.: B
	float channel2 = ((float)rawPixel.y) * 0.59f;// e.g.: G
	float channel3 = ((float)rawPixel.z) * 0.3f; // e.g.: R

	return (int4)(channel1, channel2, channel3, 0);
} 

__kernel 
void run(read_only image2d_t src, write_only image2d_t dst /*__global int* ArrayParameter, int normalParameter*/) {

	int x = get_global_id(0);
	int y = get_global_id(1);

	int4 rawPixel = read_imagei(src, sampler, (int2) (x,y));
	
	rawPixel = calcGrayScale(rawPixel);

	write_imagei(dst, (int2) (x,y), rawPixel);
}
