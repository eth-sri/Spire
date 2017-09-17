#!/bin/bash

WORK_DIR="social"
GENERATOR="./../Social generator/bin/Release/Social generator.exe"
MIN_SIZE=3
MAX_SIZE=10

if [ -d $WORK_DIR ]; then
	rm -rf $WORK_DIR
fi
mkdir $WORK_DIR
cd $WORK_DIR

for i in 3 6 9 12; do
	mono "$GENERATOR" --nodes=$i --policy=1 --program=sum --out=social-sum_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=1 --program=noise --out=social-noise_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=1 --program=subset --people=1 --out=social-subset_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=sum --out=social-sum_${i}_${i}_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=noise --out=social-noise_${i}_${i}_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=subset --people=1 --out=social-subset_${i}_${i}_
done
