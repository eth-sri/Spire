#!/bin/bash

WORK_DIR="medical"
GENERATOR="./../Medical generator/bin/Release/Medical generator.exe"
MIN_SIZE=3
MAX_SIZE=10

if [ -d $WORK_DIR ]; then
	rm -rf $WORK_DIR
fi
mkdir $WORK_DIR
cd $WORK_DIR

# 6 9 12
for i in 3 6 9; do
	mono "$GENERATOR" --nodes=$i --policy=1 --program=sum --out=medical-sum_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=1 --program=noise --out=medical-noise_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=1 --program=prevalence --out=medical-prevalence_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=1 --program=nucl --nucl-patients=1 --out=medical-nucl_${i}_1_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=sum --out=medical-sum_${i}_${i}_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=noise --out=medical-noise_${i}_${i}_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=prevalence --out=medical-prevalence_${i}_${i}_
	mono "$GENERATOR" --nodes=$i --policy=$i --program=nucl --nucl-patients=1 --out=medical-nucl_${i}_${i}_
done
