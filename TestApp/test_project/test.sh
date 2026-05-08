#!/bin/bash

rm -rf out flat
../../Compile/bin/Debug/net10.0/Compile -out out -flatts flat
find -type f | sort | xargs sha1sum >out/hash.txt
