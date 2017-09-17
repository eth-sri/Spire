#!/bin/bash

PSI_PATH=""
SYNTHESIZER="./../Spire/bin/x64/Release/Spire.exe"

NUM_PROCESSES=16
NUM_REPEATS=10
Z3_TIMEOUT=3600000
PSI_TIMEOUT=3600000

find . -type f -name "*program.psi" | sed 's/.\///' | sed 's/_program.psi//g' | while read PREFIX; do
	for run_id in $(seq 1 $NUM_REPEATS); do
		OUTPUT="${PREFIX}_run${run_id}"
		echo --psi-path="$PSI_PATH" --prior="${PREFIX}_prior.psi" --program="${PREFIX}_program.psi" --policy="${PREFIX}_policy.psi" --log="${OUTPUT}.log" --csv="${OUTPUT}.csv" --tmp-prefix="${OUTPUT}_" --z3timeout=$Z3_TIMEOUT --psitimeout=$PSI_TIMEOUT --opt-goal=singletons --smt-lib-log="{$OUTPUT}.smtlib" --iteration-log="${OUTPUT}_iteration_log.csv" ">" $OUTPUT.stdout "2>&1"
	done
done | xargs -n 9 -I{} -P $NUM_PROCESSES sh -c "mono \"$SYNTHESIZER\" {}"
