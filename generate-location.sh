#!/bin/bash

WORK_DIR="location"
GENERATOR="./../Location generator/bin/Release/Location generator.exe"
MIN_SIZE=5
MAX_SIZE=10

if [ -d $WORK_DIR ]; then
	rm -rf $WORK_DIR
fi
mkdir $WORK_DIR
cd $WORK_DIR

# 6 9 12 15 18
for i in 6 9 12 15; do
	for j in 1 3; do
		mono "$GENERATOR" --program=identity --out=location-identity_${i}_${j}_ --width=$i --height=$i --regions=$j --lower-bound=0 --upper-bound=0.5
		mono "$GENERATOR" --program=constant --out=location-constant_${i}_${j}_ --width=$i --height=$i --regions=$j --lower-bound=0 --upper-bound=0.5
		mono "$GENERATOR" --program=random --out=location-random_${i}_${j}_ --width=$i --height=$i --regions=$j --lower-bound=0 --upper-bound=0.5
		mono "$GENERATOR" --program=random-smart --out=location-random-smart_${i}_${j}_ --width=$i --height=$i --regions=$j --lower-bound=0 --upper-bound=0.5
	done
done
