#!/bin/bash

for f in *.csv; do cat $f; echo ""; done | grep ",$" | column -s, -t
