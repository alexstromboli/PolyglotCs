#!/bin/bash

rm -rf out flat
../../Compile/bin/Debug/net5.0/Compile -out out -flatts flat
find -type f | sort | xargs sha1sum >out/hash.txt
