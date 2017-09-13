#!/bin/bash

mkdir evaluation

for f in medical-sum*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/medical-sum.csv
for f in medical-nucl*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/medical-nucl.csv
for f in medical-noise*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/medical-noise.csv
for f in medical-prevalence*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/medical-prevalence.csv
