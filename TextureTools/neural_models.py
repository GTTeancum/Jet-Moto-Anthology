# Modified inference-only adaptation; checkpoint-compatible forward operations.
# SRVGG portions: Copyright (c) 2021, Xintao Wang, BSD-3-Clause.
# RRDB portions: Copyright 2018-2022 BasicSR Authors, Apache-2.0.
# Changes: fixed x4 shapes; removed training/registry initialization; safe loading.
"""Inference-only architectures compatible with official Real-ESRGAN checkpoints.
Forward definitions follow upstream SRVGGNetCompact (Real-ESRGAN) and RRDBNet
(BasicSR). No training, face enhancer, style-transfer model, or downloaded code.
Source references and upstream licenses are recorded with the release.
"""
import torch
from torch import nn
from torch.nn import functional as F

class SRVGGNetCompact(nn.Module):
    def __init__(self):
        super().__init__()
        self.body = nn.ModuleList([nn.Conv2d(3,64,3,1,1),nn.PReLU(64)])
        for _ in range(32):
            self.body.extend([nn.Conv2d(64,64,3,1,1),nn.PReLU(64)])
        self.body.append(nn.Conv2d(64,48,3,1,1))
        self.upsampler = nn.PixelShuffle(4)
    def forward(self,x):
        y=x
        for layer in self.body:y=layer(y)
        return self.upsampler(y)+F.interpolate(x,scale_factor=4,mode='nearest')

class ResidualDenseBlock(nn.Module):
    def __init__(self):
        super().__init__()
        self.conv1=nn.Conv2d(64,32,3,1,1)
        self.conv2=nn.Conv2d(96,32,3,1,1)
        self.conv3=nn.Conv2d(128,32,3,1,1)
        self.conv4=nn.Conv2d(160,32,3,1,1)
        self.conv5=nn.Conv2d(192,64,3,1,1)
        self.lrelu=nn.LeakyReLU(0.2,inplace=True)
    def forward(self,x):
        values=[x]
        for i in range(1,5): values.append(self.lrelu(getattr(self,'conv'+str(i))(torch.cat(values,1))))
        return x+0.2*self.conv5(torch.cat(values,1))

class RRDB(nn.Module):
    def __init__(self):
        super().__init__();self.rdb1=ResidualDenseBlock();self.rdb2=ResidualDenseBlock();self.rdb3=ResidualDenseBlock()
    def forward(self,x):return x+0.2*self.rdb3(self.rdb2(self.rdb1(x)))

class RRDBNet(nn.Module):
    def __init__(self):
        super().__init__()
        self.conv_first=nn.Conv2d(3,64,3,1,1);self.body=nn.Sequential(*[RRDB() for _ in range(23)])
        self.conv_body=nn.Conv2d(64,64,3,1,1);self.conv_up1=nn.Conv2d(64,64,3,1,1)
        self.conv_up2=nn.Conv2d(64,64,3,1,1);self.conv_hr=nn.Conv2d(64,64,3,1,1);self.conv_last=nn.Conv2d(64,3,3,1,1)
        self.lrelu=nn.LeakyReLU(0.2,inplace=True)
    def forward(self,x):
        x=self.conv_first(x);x=x+self.conv_body(self.body(x))
        x=self.lrelu(self.conv_up1(F.interpolate(x,scale_factor=2,mode='nearest')))
        x=self.lrelu(self.conv_up2(F.interpolate(x,scale_factor=2,mode='nearest')))
        return self.conv_last(self.lrelu(self.conv_hr(x)))

def load_model(weights,kind='general',denoise=0.25):
    if kind=='rrdb':
        model=RRDBNet();state=torch.load(weights/'RealESRGAN_x4plus.pth',map_location='cpu',weights_only=True)['params_ema']
    else:
        model=SRVGGNetCompact()
        strong=torch.load(weights/'realesr-general-x4v3.pth',map_location='cpu',weights_only=True)['params']
        weak=torch.load(weights/'realesr-general-wdn-x4v3.pth',map_location='cpu',weights_only=True)['params']
        state={k:strong[k]*denoise+weak[k]*(1-denoise) for k in strong}
    model.load_state_dict(state,strict=True)
    return model.eval().requires_grad_(False)
