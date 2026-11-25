"""小番茄像素混淆。Curve adapted from jakubcerveny/gilbert, shift per iris10086/pic-scramble."""
from functools import lru_cache
from math import sqrt

import numpy as np
import torch


def _curve(x, y, ax, ay, bx, by, stride):
    """Yield flat pixel indices along an oriented Gilbert rectangle."""
    width, height = abs(ax + ay), abs(bx + by)
    dx, dy, ex, ey = ((v > 0) - (v < 0) for v in (ax, ay, bx, by))

    if height == 1 or width == 1:
        step = dx + dy * stride if height == 1 else ex + ey * stride
        start = x + y * stride
        yield from range(start, start + step * max(width, height), step)
        return

    ax2, ay2, bx2, by2 = ax // 2, ay // 2, bx // 2, by // 2
    if 2 * width > 3 * height:
        if abs(ax2 + ay2) % 2 and width > 2:
            ax2 += dx
            ay2 += dy
        yield from _curve(x, y, ax2, ay2, bx, by, stride)
        yield from _curve(x + ax2, y + ay2, ax - ax2, ay - ay2, bx, by, stride)
    else:
        if abs(bx2 + by2) % 2 and height > 2:
            bx2 += ex
            by2 += ey
        yield from _curve(x, y, bx2, by2, ax2, ay2, stride)
        yield from _curve(x + bx2, y + by2, ax, ay, bx - bx2, by - by2, stride)
        yield from _curve(
            x + ax - dx + bx2 - ex, y + ay - dy + by2 - ey,
            -bx2, -by2, ax2 - ax, ay2 - ay, stride,
        )


@lru_cache(maxsize=1)
def _curve_indices(width, height):
    axes = (width, 0, 0, height) if width >= height else (0, height, width, 0)
    return np.fromiter(_curve(0, 0, *axes, width), dtype=np.intp, count=width * height)


class CherryTomatoScramble:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {
            "image": ("IMAGE",),
            "encrypt": ("BOOLEAN", {"default": True, "label_on": "加密", "label_off": "解密"}),
        }}

    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "transform"
    CATEGORY = "image"
    DESCRIPTION = "打开开关加密，关闭开关解密。支持批量图片。"

    def transform(self, image, encrypt):
        batch, height, width, channels = image.shape
        size = height * width
        if size <= 1:
            return (image,)
        curve = _curve_indices(width, height)
        offset = int((sqrt(5) - 1) / 2 * width * height + 0.5)
        # Gather from the preceding curve position to encrypt, the following to decrypt.
        source = np.empty(size, dtype=np.int64)
        source[curve] = np.roll(curve, offset if encrypt else -offset)
        indices = torch.from_numpy(source).to(image.device)
        pixels = image.reshape(batch, size, channels)
        return (pixels.index_select(1, indices).reshape_as(image),)


NODE_CLASS_MAPPINGS = {"CherryTomatoScramble": CherryTomatoScramble}
NODE_DISPLAY_NAME_MAPPINGS = {"CherryTomatoScramble": "小番茄图片加密 / 解密"}

# BSD 2-Clause License
# Copyright (c) 2018, Jakub Červený
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions are met:
# * Redistributions of source code must retain the above copyright notice, this
#   list of conditions and the following disclaimer.
# * Redistributions in binary form must reproduce the above copyright notice,
#   this list of conditions and the following disclaimer in the documentation
#   and/or other materials provided with the distribution.
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
# AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
# IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
# FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
# DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
# SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
# CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
# OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
# OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#
# MIT License
# Copyright (c) 2026 iris10086
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
# SOFTWARE.
