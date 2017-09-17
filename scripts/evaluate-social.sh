#!/bin/bash

mkdir evaluation

for f in social-sum*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/social-sum.csv
for f in social-subset*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/social-subset.csv
for f in social-noise*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/social-noise.csv
