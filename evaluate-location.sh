#!/bin/bash

mkdir evaluation

for f in location-constant*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/location-constant.csv
for f in location-identity*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/location-identity.csv
for f in location-random_*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/location-random.csv
for f in location-random-smart*.csv; do cat $f; echo ""; done | grep ",$" > evaluation/location-random-smart.csv
